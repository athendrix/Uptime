using System;
using System.Threading.Tasks;
using Bridge;
using Bridge.Html5;

namespace UptimeClient
{
    public struct ServiceModel
    {
        public ServiceModel(string name, string address, string displayaddress, bool external, string backend, string live, bool trustcert, int checktype, string checktime)
        {
            this.name = name;
            this.address = address;
            this.displayaddress = displayaddress;
            this.external = external;
            this.backend = backend;
            this.live = live;
            this.trustcert = trustcert;
            this.checktype = checktype;
            this.checktime = checktime;
        }
        public readonly string name;
        public readonly string address;
        public readonly string displayaddress;
        public readonly bool external;
        public readonly string backend;
        public readonly string live;
        public readonly bool trustcert;
        public readonly int checktype;
        public readonly string checktime;
    }
    public class Program
    {
        private static HTMLDivElement LivenessIndicator = null;
        public static async Task UpdateLiveness()
        {
            bool Live = await BridgeAssist.Helpers.Check("/api/Live");
            if(Live)
            {
                if(Errors)
                {
                    LivenessIndicator.TextContent = "Server Connection Live With Errors!";
                    LivenessIndicator.Style.BackgroundColor = "yellow";
                }
                else
                {
                    LivenessIndicator.TextContent = "Server Connection Live!";
                    LivenessIndicator.Style.BackgroundColor = "green";
                }
                
            }
            else
            {
                LivenessIndicator.TextContent = "Server Connection Down!";
                LivenessIndicator.Style.BackgroundColor = "red";
            }
        }
        public static void Main(string[] args)
        {
            Document.Body.AppendChild(new HTMLHeadingElement(HeadingType.H1) { TextContent = "Services" });
            LivenessIndicator = new HTMLDivElement();
            Document.Body.AppendChild(LivenessIndicator);
            Table = new HTMLTableElement();
            Document.Body.AppendChild(Table);
            Tick();
        }
        private static HTMLTableElement Table;
        private static bool Errors = false;
        private static long count = 0;
        private static ServiceModel[] Services = null;
        public static async void Tick()
        {
            try
            {
                count++;
                if (count % 3 == 0 || Services == null)
                {
                    await UpdateLiveness();
                    Services = await BridgeAssist.Helpers.Get<ServiceModel[]>("/api/ServiceList");
                }
                Errors = false;
                Table.InnerHTML = "";
                HTMLTableRowElement Header = new HTMLTableRowElement();
                Header.AppendChild(new HTMLTableHeaderCellElement() { TextContent = "Service" });
                Header.AppendChild(new HTMLTableHeaderCellElement() { TextContent = "Address" });
                Header.AppendChild(new HTMLTableHeaderCellElement() { TextContent = "Backend" });
                Header.AppendChild(new HTMLTableHeaderCellElement() { TextContent = "Internal/External" });
                Header.AppendChild(new HTMLTableHeaderCellElement() { TextContent = "Status" });
                Header.AppendChild(new HTMLTableHeaderCellElement() { TextContent = "Uptime" });
                Table.AppendChild(Header);

                for (int i = 0; i < Services.Length; i++)
                {
                    HTMLTableRowElement Row = new HTMLTableRowElement();
                    Row.AppendChild(new HTMLElement("td") { TextContent = Services[i].name });
                    string address = Services[i].displayaddress ?? Services[i].address;
                    HTMLElement AddressCell = new HTMLElement("td");
                    bool http = address.StartsWith("http");
                    bool https = address.StartsWith("https://");
                    if (http || https)
                    {
                        AddressCell.AppendChild(new HTMLAnchorElement() { Href = address, TextContent = (https ? "🔐" : "☠️") + address, Target = "_blank", Rel = "noopener noreferrer" });
                    }
                    else
                    {
                        AddressCell.TextContent = address;
                    }
                    
                    Row.AppendChild(AddressCell);
                    Row.AppendChild(new HTMLElement("td") { TextContent = Services[i].backend });
                    Row.AppendChild(new HTMLElement("td") { TextContent = Services[i].external ? "External" : "Internal" });
                    Row.AppendChild(new HTMLElement("td") { TextContent = Services[i].live });
                    if(Services[i].live.ToLower().Contains("ok"))
                    {
                        Row.AppendChild(new HTMLElement("td") { TextContent = FormatUptime(DateTime.Parse(Services[i].checktime).Ticks) });
                    }
                    else
                    {
                        Row.AppendChild(new HTMLElement("td") { TextContent = "DOWN" });
                    }
                    
                    Table.AppendChild(Row);
                }
            }
            catch(Exception)
            {
                Errors = true;
            }
            Window.SetTimeout(Tick, 1000);
        }

        public static string FormatUptime(long ticks)
        {
            long seconds = (DateTime.Now.Ticks - ticks)/10000000;
            long days = seconds / (60 * 60 * 24);
            seconds = seconds % (60 * 60 * 24);
            long hours = seconds / (60 * 60);
            seconds = seconds % (60 * 60);
            long minutes = seconds / 60;
            seconds = seconds % 60;
            string daypart = days > 0 ? (days + (days == 1 ? " Day ":" Days ")) : "";
            string hourpart = hours > 0 ? (hours + (hours == 1 ?" Hour ":" Hours ")) : "";
            string minutepart = minutes > 0 ? (minutes + (minutes == 1 ? " Minute " : " Minutes ")) : "";
            return  daypart + hourpart + minutepart + seconds + (seconds == 1 ? " Second" : " Seconds");
        }
    }
}
