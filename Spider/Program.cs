using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Web;
using Helper;
using SufeiUtil;
using System.Threading;

namespace Spider
{
    public class Program
    {
        public static string keyWord = string.Empty;
        public static string page = string.Empty;
        public static string filePath = @"D:\テスト\SpiderFileTest";
        public static int bookmarkCount = 30;
        public static string loginId = string.Empty;
        public static string passwd = string.Empty;

        public static void Main(string[] args)
        {
            Stopwatch sw=new Stopwatch();
            sw.Start();
            HttpRequestHelper helper = new HttpRequestHelper();
            //不登录也可以，不过不能通过点赞数筛选图片
            Console.Write("账号：");
            loginId = Console.ReadLine();
            Console.Write("密码：");
            passwd = Console.ReadLine();
            Console.Write("请输入关键字：");
            keyWord = Console.ReadLine();
            Console.Write("请输入爬取的页数：");
            page = Console.ReadLine();
            Console.Write("保存点赞数大于X的：");
            var bmCount = Console.ReadLine();
            if (!bmCount.IsNullOrEmpty())
            {
                bookmarkCount = bmCount.ToInt();
            }
            Console.Write("输入保存路径：");
            var path = Console.ReadLine();
            if (!path.IsNullOrEmpty())
            {
                filePath = path;
            }

            InputVerify();
            var url = "https://www.pixiv.net/search.php?word={0}&order=date_d&p={1}";
            helper.PixivLogin(loginId,passwd);
            for (var i = 1; i <= page.ToInt(); i++)
            { 
                var uri = string.Format(url, HttpUtility.UrlEncode(keyWord), i);
                helper.url = uri;
                helper.filePath = filePath;
                helper.bookmarkCount = bookmarkCount;
                helper.GetImage();
            }
            sw.Stop();
            Console.WriteLine("共用时：{0}秒",sw.Elapsed.TotalSeconds);
            Console.ReadLine();
        }

        public static void InputVerify()
        {
            keyWord=keyWord.keyWordVerify("请输入关键字：");

            page = page.keyWordVerify("请输入页数");

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


    }
}
