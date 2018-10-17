using System;

using System.Net;
using System.Text;
using System.Xml.XPath;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace GetDisplay
{
    class Program
    {
        public static Uri m_uriBase { get; set; }
        public static string m_width { get; set; }
        public static string m_height { get; set; }
        public static string m_txtboxWidth { get; set; }
        public static string m_scrollerWidth { get; set; }
        public static string m_ipAddress { get; set; }
        public static string m_display_name { get; set; }

        static System.Threading.ManualResetEvent resetEvent = new ManualResetEvent(false);

        public const string mUsage = "Enter IP address in a format of -ip xxx.yyy.zzz.fff or --ip xxx.yyy.zzz.fff\n";
        static void Main(string[] args)
        {
            bool helpRequest = false;
            
            if (args.Length == 0)
            {
                System.Console.WriteLine(mUsage);
                return;
            }

            for (int i = 0; i < args.Length; ++i)
            {
                switch (args[i])
                {
                    case "--help":
                    case "-h":
                    case "--h":
                    case "-help":
                        helpRequest = true;
                        Console.WriteLine(mUsage);
                        break;
                    case "-ip":
                    case "--ip":
                        m_ipAddress = args[++i];
                        break;
                    default:
                        System.Console.WriteLine(mUsage);
                        return;
                }
            }

            if (helpRequest)
            {
                return;
            }

            if (m_ipAddress.Length == 0)
            {
                System.Console.WriteLine("Please enter IP address");
                return;
            }

            Regex ip = new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");
            MatchCollection result = ip.Matches(m_ipAddress);

            if (result.Count >= 1)
            {
                Console.WriteLine("IP address keyed in:" + result[0] + "\r\n");
            }
            else
            {
                Console.WriteLine(mUsage);
                return;
            }
            

            m_uriBase = new Uri("http://"+ m_ipAddress);

            GetResponse(new Uri(m_uriBase,"/displays/$all"), OnDisplaysResponse);

            resetEvent.WaitOne();
        }


        private static void OnDisplaysResponse(String rsp)
        {
            // The response contains a list of display names. As this driver only supports one display per Daktronics REST server, we just pull the name from the first list item.

            Regex rex = new Regex("\\{\\s*\"([^\"]*)\"");       // {<whitespace>"DisplayName" ...
            Match m = rex.Match(rsp);
            try
            {
                m_display_name = m.Groups[1].Value;
                ParsePropertyFile();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }

        private static void GetResponse(Uri uri, Action<String> callback)
        {
            WebClient wc = new WebClient();
            wc.OpenReadCompleted += (o, a) =>
            {
                if (callback != null)
                {
                    try
                    {
                        if (a.Result != null)
                        {
                            using (var reader = new StreamReader(a.Result, Encoding.UTF8))
                            {
                                callback(reader.ReadToEnd());
                            }
                            a.Result.Close();
                        }
                    }
                    catch (System.Reflection.TargetInvocationException)
                    {  // Unable to connect to server ... handled by retries in the main state machine
                    }
                }
            };
            wc.OpenReadAsync(uri);
        }

        private static void ParsePropertyFile()
        {
            Get("displays/" + m_display_name + ".display$settings");

        }

        private static void Get(string path_)
        {
            byte[] mDataBuffer;
            string download;
            string path;

            try
            {
                WebClient wc = new WebClient();

                path = m_uriBase.ToString() + path_;
                mDataBuffer = wc.DownloadData(path);
                download = Encoding.ASCII.GetString(mDataBuffer);

                var jsonReader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(download), new System.Xml.XmlDictionaryReaderQuotas());
                var root = System.Xml.Linq.XElement.Load(jsonReader);
                m_width = root.XPathSelectElement("//geometry/width").Value;
                m_height = root.XPathSelectElement("//geometry/height").Value;

                m_txtboxWidth = (Convert.ToInt16((Convert.ToInt16(m_height) * 52) / 32)).ToString();
                m_scrollerWidth = (Convert.ToInt16(m_width) - Convert.ToInt16(m_txtboxWidth)).ToString();

                Console.WriteLine("=================Display properties=================");

                Console.WriteLine("Sign name:" + m_display_name);
                Console.WriteLine("Sign width:" + m_width);
                Console.WriteLine("Sign height:" + m_height);
                Console.WriteLine("text box width:" + m_txtboxWidth);
                Console.WriteLine("scroller width:" + m_scrollerWidth);

                Console.WriteLine("=================The end=================");


                resetEvent.Set();
            }
            catch (WebException e)
            {
                string s = e.Message;

            }

        }
    }
}
