using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using HtmlAgilityPack;
using Newtonsoft.Json;
using SufeiUtil;
using System.Web;

namespace Helper
{
    public class HttpRequestHelper
    {
        private string proxyIp = "127.0.0.1:2160";

        private string useragent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/75.0.3770.100 Safari/537.36";

        private int retryTimes = 3;
        private int Count = 0;
        public string url = "";
        public string filePath = "";
        private string cookie = "";
        private string loginCookie = "";
        public int bookmarkCount = 100;
        HttpHelper http = new HttpHelper();
        HttpItem item=new HttpItem();

        public void PixivLogin(string loginId,string passwd)
        {
            if (loginId.IsNullOrEmpty() || passwd.IsNullOrEmpty())
            {
                Console.WriteLine("账号或密码为空，无法登陆，筛选功能无法使用");
                return;
            }

            var loginUrl = "https://accounts.pixiv.net/api/login";
            var refererUrl = "https://accounts.pixiv.net/login";
            var loginData= GetLoginData();
            var postDataStr = "captcha=&g_recaptcha_response=&post_key={0}&pixiv_id={1}&password={2}&source=accounts&ref=&return_to=https://www.pixiv.net/&recaptcha_v3_token=";//
            var postData = string.Format(postDataStr, loginData["postKey"], loginId, passwd);
            HttpItem item1 = new HttpItem()
            {
                URL = loginUrl,
                Method = "post",
                ProxyIp = proxyIp,
                UserAgent = useragent,
                Referer = "https://accounts.pixiv.net/login?lang=zh&source=pc&view_type=page&ref=wwwtop_accounts_index",
                ContentType = "application/x-www-form-urlencoded",
                Accept = "application/json",
                Postdata = postData,
                Allowautoredirect =true,
                Cookie = loginCookie
            };
            item1.Header.Add("authority", "accounts.pixiv.net");
            item1.Header.Add("origin", "https://accounts.pixiv.net");
            item1.Header.Add("dnt", "1");
            item1.Header.Add("path", "/api/login?lang=zh");

            
            var res = http.GetHtml(item1);
            if (res.StatusCode == HttpStatusCode.OK)
            {
                cookie = res.Cookie;//保存登录后的cookie
                Console.WriteLine("登录成功！");
            }
            else
            {
                if (Count <= retryTimes)
                {
                    Count++;
                    Console.WriteLine("登录失败，第{0}次重试中。。。", Count);
                    PixivLogin(loginId, passwd);
                }
                Console.WriteLine("登录失败，按任意键退出！");
                Console.Read();
                Process.GetCurrentProcess().Kill();
            }

            Count = 0;
        }

        /// <summary>
        /// 获取图片主方法
        /// </summary>
        public void GetImage()
        {
            VerifyFile(filePath);
            item = new HttpItem()
            {
                URL=url,
                Method = "get",
                ContentType = "text/html",
                ProxyIp = proxyIp,
                Referer = url,
                UserAgent = useragent,
                Cookie = cookie
            };
            var result = http.GetHtml(item);
            if (result.StatusCode != HttpStatusCode.OK)
            {
                if (Count <= retryTimes)
                {
                    Count++;
                    GetImage();
                }
            }
            var json = GetDataJson(result.Html);
            var imageId = GetImageId(json, bookmarkCount);
            var dic = new Dictionary<string, object>();
            var num = DownloadImage(imageId);
            Count = 0;
        }

        /// <summary>
        /// 根据点赞数筛选图片
        /// </summary>
        /// <param name="str"></param>
        /// <param name="bookmarkCount"></param>
        /// <returns></returns>
        private List<string> GetImageId(string str, int bookmarkCount)
        {
            var list = new List<string>();
            if (!str.IsNullOrEmpty())
            {
                var jarr = JArray.Parse(str);

                foreach (var arr in jarr)
                {
                    var num = arr["bookmarkCount"].ToInt();
                    if (num >= bookmarkCount)
                    {
                        var imageId = arr["illustId"].ToString();
                        list.Add(imageId);
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// 批量下载图片，对于图片的合集使用并行类下载
        /// </summary>
        /// <param name="imageIdList"></param>
        /// <returns></returns>
        private int DownloadImage(List<string> imageIdList)
        {
            var url = "https://www.pixiv.net/ajax/illust/[Replace]/pages";
            var referer = "https://www.pixiv.net/member_illust.php?mode=medium&illust_id=[Replace]";
            var i = 0;
            var retryCount = 0;
            for (var m = 0; m < imageIdList.Count; m++)
            {
                var imageId = imageIdList[m];
                var uri = url.Replace("[Replace]", imageId);
                var refererUri = referer.Replace("[Replace]", imageId);
                item = new HttpItem()
                {
                    URL = uri,
                    Method = "get",
                    ContentType = "text/html",
                    ProxyIp = proxyIp,
                    UserAgent = useragent,
                    Cookie = cookie,
                };
                var result = http.GetHtml(item);
                if (result.StatusCode != HttpStatusCode.OK)
                {
                    retryCount++;
                    if (retryCount < retryTimes)
                    {
                        Console.WriteLine("图片获取失败，原因：{0}", result.Html);
                        m--;
                        Console.WriteLine("尝试第{0}次重新获取图片", retryCount);
                        continue;
                    }
                    else
                    {
                        Console.WriteLine("当前图片下载失败；");
                        continue;
                    }
                }

                var jobj = JObject.Parse(result.Html);
                var json = (JArray) jobj["body"];
                var count = 0;
                try
                {
                    Parallel.ForEach(json, items =>
                    {
                        var urls = items["urls"];
                        var imgUrl = urls["original"].ToString();
                        var nameArr = imgUrl.Split('/');
                        var name = nameArr[nameArr.Length - 1];
                        var fileName = string.Format("{0}/{1}", filePath, name);
                        var title = GetTitle(refererUri);
                        title = fileNameVerify(title);
                        if (!title.IsNullOrEmpty() && json.Count > 1)
                        {
                            VerifyFile(string.Format("{0}/{1}", filePath, title));
                            fileName = string.Format("{0}/{1}/{2}", filePath, title, name);
                        }
                        else if(!title.IsNullOrEmpty())
                        {
                            fileName = string.Format("{0}/{1}", filePath, title + Path.GetExtension(name));
                        }
                        else
                        {
                            fileName = name;
                        }

                        if (isImageExist(fileName))//文件存在直接返回
                        {
                            Console.WriteLine("文件存在；");
                            return;
                        }

                        item = new HttpItem()
                        {
                            URL = imgUrl,
                            Method = "get",
                            ResultType = ResultType.Byte,
                            Referer = refererUri,

                        };
                        Console.WriteLine("开始下载图片{0}/{1}", title, name);
                        Stopwatch sw = new Stopwatch();
                        sw.Start();
                        var imageBytes = http.GetHtml(item).ResultByte;
                        if (imageBytes == null) return;//转换失败的图片直接返回
                        sw.Stop();
                        Console.WriteLine("图片{1}/{2}下载完成，使用时间{0}毫秒", sw.Elapsed.Milliseconds, title, name);
                        var image = GetImage(imageBytes);
                        image.Save(fileName);
                        Console.WriteLine("抓取图片{0}/{1}成功", title, name);
                        i++;
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

            Console.WriteLine("共抓取{0}张图", i);
            return i;
        }

        /// <summary>
        ///获取页面的图片信息
        /// </summary>
        /// <param name="str">网页字符串</param>
        /// <returns>过滤后的结果</returns>
        private string GetDataJson(string str)
        {
            var result = string.Empty;
            var xpath = @"//input[@id='js-mount-point-search-result-list']";
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(str);
            var nodes = doc.DocumentNode.SelectSingleNode(xpath);
            if (nodes != null)
            {
                var json = nodes.Attributes["data-items"].Value;
                result = json.Replace("&quot;", "\"");
            }
            return result;
        }

        /// <summary>
        /// 获取作者对作品的名字，用于对下载后的作品命名
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private string GetTitle(string url)
        {
            var result = string.Empty;
            item=new HttpItem()
            {
                URL=url,
                Method = "get",
                ProxyIp = proxyIp,
                UserAgent = useragent,
                Cookie = cookie
            };
            var res = http.GetHtml(item);
            if (res.StatusCode == HttpStatusCode.OK)
            {
                var xpath = @"//meta[@property='twitter:title']";
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(res.Html);
                var nodes = doc.DocumentNode.SelectSingleNode(xpath);
                if (nodes != null)
                {
                    var json = nodes.Attributes["content"].Value;
                    result = json;
                }
            }
            return result;
        }

        /// <summary>
        /// 获取登录需要用的post_key与cookie
        /// </summary>
        /// <returns></returns>
        private Dictionary<string,object> GetLoginData()
        {
            var result = new Dictionary<string,object>();
            item = new HttpItem()
            {
                URL = "https://accounts.pixiv.net/login",
                Method = "get",
                ProxyIp = proxyIp,
                UserAgent = useragent,
            };
            var res = http.GetHtml(item);
            if (res.StatusCode == HttpStatusCode.OK)
            {
                loginCookie = res.Cookie;
                var xpath = @"//input[@name='post_key']";
                HtmlDocument doc=new HtmlDocument();
                doc.LoadHtml(res.Html);
                var postKey = doc.DocumentNode.SelectSingleNode(xpath);
                if (postKey != null)
                {
                    result["postKey"] = postKey.Attributes["value"].Value;
                }
            }
            return result;
        }

        /// <summary>
        /// 根据名字判断图片是否存在
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private bool isImageExist(string name)
        {
            bool result = false;
            if (File.Exists(name))
            {
                result = true;
            }
            return result;

        }

        /// <summary>
        /// 字节数组生成图片
        /// </summary>
        /// <param name="Bytes">字节数组</param>
        /// <returns>图片</returns>
        private Image GetImage(byte[] Bytes)
        {
            MemoryStream ms = new MemoryStream(Bytes);
            return Bitmap.FromStream(ms, true);
        }

        /// <summary>
        /// 验证文件夹是否存在，不存在则创建
        /// </summary>
        /// <param name="filePath"></param>
        public void VerifyFile(string filePath)
        {
            if (!Directory.Exists(filePath))
            {
                try
                {
                    Directory.CreateDirectory(filePath);
                }
                catch (Exception ex)
                {
                    Console.Write(ex);
                }
            }
        }

        /// <summary>
        /// 替换文件中的非法字符，非法字符全部替换为“_”
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public string fileNameVerify(string fileName)
        {
            var res = fileName;
            var charArr = Path.GetInvalidFileNameChars();
            if (fileName.IndexOfAny(charArr)>0)
            {
                res = Path.GetInvalidFileNameChars().Aggregate(fileName, (p, c) => p.Replace(c, '_'));
            }
            return res;
        }
    }
}