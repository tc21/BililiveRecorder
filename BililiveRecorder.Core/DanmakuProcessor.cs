using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace BililiveRecorder.Core
{
    /* TODO
     * - RecordedRoom 最下方的改动，是为了弹幕和视频名字相同，但是怎么看怎么不好看
     * - 最好用 XDocument 之类的正经 XML Writer，至少加个 HTMLEncode 之类的
     */

    // very roughly written since I couldn't be assed to make it better
    class DanmakuXMLDocument
    {
        //<d p="在视频中的秒浮点数,弹幕类型,弹幕字号,十进制RGB颜色,发送UNIX时间戳,弹幕池保持0普通,hash后的发送者id,数据库id">弹幕内容</d>
        //<d p = "1.588,        1,      25,    16777215,    1548576075,  0,            b86462f0,      0" > 就休息了5分钟，人太多了</d>
        //[0, 1, 25, 16777215, 1540904619, -406758470, 0, "f20e41ac", 0, 0],
        private readonly int fontSize;
        private static readonly string preamble = string.Join("\n", new string[] {
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
            "<i>",
            "<chatserver>chat.bilibili.com</chatserver>",
            "<chatid>0</chatid>",
            "<mission>0</mission>",
            "<maxlimit>0</maxlimit>",
            "<source>k-v</source>"
        });

        private static readonly string conclusion = "</i>";

        private List<string> lines = new List<string>();

        public DanmakuXMLDocument(int fontSize = 25)
        {
            this.fontSize = fontSize;
        }

        public void Add(DanmakuModel danmaku, double secondsElapsed)
        {
            var json = JObject.Parse(danmaku.RawData);
            var metadata = json["info"][0];

            var danmakuType = metadata[1].ToObject<int>();
            var fontSize = metadata[2].ToObject<int>();
            var colorCode = metadata[3].ToObject<int>();
            var unixTimestamp = metadata[4].ToObject<int>();
            var senderId = metadata[7].ToObject<string>();

            string p = string.Format(
                "{0},{1},{2},{3},{4},0,{5},0",
                secondsElapsed,
                danmakuType,
                fontSize,
                colorCode,
                unixTimestamp,
                senderId
            );

            lines.Add(string.Format("<d p=\"{0}\">{1}</d>", p, danmaku.CommentText));
        }

        public void WriteTo(StreamWriter writer)
        {
            writer.WriteLine(preamble);
            foreach (var line in lines)
            {
                writer.WriteLine(line);
            }
            writer.WriteLine(conclusion);
        }
    }

    public class DanmakuStreamProcessor : IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly string danmakuFileName;
        private readonly DateTime startTime;

        private DanmakuXMLDocument document = new DanmakuXMLDocument();

        public DanmakuStreamProcessor(string danmakuFileName, IStreamMonitor monitor)
        {
            this.danmakuFileName = danmakuFileName;
            this.startTime = DateTime.Now;
            monitor.ReceivedDanmaku += ReceivedDanmakuHandler;
        }

        public void FinalizeFile()
        {
            try
            {
                logger.Debug("正在写入弹幕文件: " + danmakuFileName);
                try { Directory.CreateDirectory(Path.GetDirectoryName(danmakuFileName)); } catch (Exception) { }
                using (StreamWriter writer = new StreamWriter(danmakuFileName))
                {
                    document.WriteTo(writer);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "保存弹幕文件时出错");
            }
        }

        public void Dispose()
        {
            // I don't think we actually need to do anything...
        }

        private void ReceivedDanmakuHandler(object sender, ReceivedDanmakuArgs e)
        {
            if (e.Danmaku.MsgType == MsgTypeEnum.Comment)
            {
                document.Add(e.Danmaku, DateTime.Now.Subtract(startTime).TotalSeconds);
            }
        }
    }
}
