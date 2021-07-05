using System;
using Bridge;
using Bridge.Html5;

namespace UptimeClient
{
    public class Program
    {
        public static async void Main(string[] args)
        {
            string HW = await BridgeAssist.Helpers.Get<string>("/api/HelloWorld");
            Bridge.Html5.Document.Body.AppendChild(new HTMLHeadingElement(HeadingType.H1){TextContent = HW});
        }
    }
}
