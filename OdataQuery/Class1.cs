    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System;
    using System.Xml;
    
    using System.Globalization;
      
    using System.Security;
    using System.ServiceModel;
    using System.Net;
    using System.Security.Principal;
    using System.Runtime.Serialization;
    using System.Diagnostics;
    using Citrix.Dmc.Common;
    using Citrix.Dmc.Common.Utilities;
    using Citrix.Dmc.Common.Plugin;
    using Citrix.Dmc.Connector;
    using Citrix.Dmc.WebService;
    using Citrix.Dmc.WebService.Utilities;
    using Citrix.Dmc.Connector.Broker;
    using Citrix.Dmc.Resources.Service;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System.ServiceModel.Web;
    using System.Web;
    using System.IO;
    using System.Threading.Tasks;

namespace OdataQuery
{
    
        /* change DesktopGroup usage calculation based on single and multisession*/

        [DataContract]
        public class OdataReturn
        {
           
            [DataMember]            
           public string[][] response;
           public OdataReturn(string[][] res)
           {
               response = res;
           }
           
           

        }

   

        [PluginAttribute]
        public interface pluginDllInterafce
        {
            [OperationContract]
            [FaultContract(typeof(DmcServiceFault))]
            [WebInvoke(ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Wrapped)]
            String MakeOdataQuery(String ReportName, string SiteId, String Query);
        }


        public class PluginTest : pluginDllInterafce,IDisposable
        {


            LogOnSession sessionData;

            MemoryStream PopulateChart(String Query, String odatastr)
            {
                
                MemoryStream stream = new MemoryStream();
                StreamWriter csvWriter = new StreamWriter(stream, Encoding.UTF8);
                String[][] CSVValues = ConvertTo2DArray(Query, odatastr);
                
                foreach (string[] colomns in CSVValues)
                {
                    string row = "";

                    foreach (string cell in colomns)
                    {
                        row += cell + ",";

                    }
                    csvWriter.WriteLine(row);
                    try
                    {
                       // EventLog.WriteEntry("odataquery", row);
                    }
                    catch { }
                }
                csvWriter.Flush();
                stream.Flush();
                return stream;
            }



           

            static string[][] ConvertTo2DArray(String Query, String odatastr)
            {


                //wc.Headers.Add("Accept", "application/xml");
              //  EventLog.WriteEntry("Odataquery", "inside convert to 2d ");
                System.Text.RegularExpressions.Match Match = System.Text.RegularExpressions.Regex.Match(Query, @"\S*select\S*&", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                Query = Match.ToString().Replace("&", "");
                String fields = Query.Split('=')[Query.Split('=').Length - 1];
                string[] splittedfields = fields.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                int NoOfColoumns = splittedfields.Length;
                //EventLog.WriteEntry("Odataquery", ""+NoOfColoumns);
                //EventLog.WriteEntry("Odataquery", "before" + odatastr);
                JObject O = JObject.Parse(odatastr);
                //EventLog.WriteEntry("Odataquery", "" + odatastr);
                JArray JO = (JArray)O["value"];
                //EventLog.WriteEntry("Odataquery", "after value:" + odatastr);
                //get the first field
                string FirstField = "";
                if (splittedfields[0].Contains("/"))
                {
                    FirstField = splittedfields[0].Split('/')[splittedfields[0].Split('/').Length - 1];
                }
                else
                {
                    FirstField = splittedfields[0];
                }
                //EventLog.WriteEntry("Odataquery", "" + NoOfColoumns);
                int NoOfRows = JO.Count + 1;//OdataRows.Count + 1;
                String[][] OdataTable = new String[NoOfRows][];
                int i = 0;
                OdataTable[0] = new string[NoOfColoumns];
                foreach (string field in splittedfields)
                {
                    if (field.Contains("/"))
                    {
                        OdataTable[0][i] = field.Split('/')[field.Split('/').Length - 1];
                    }
                    else
                    {
                        OdataTable[0][i] = field;
                    }

                  //  EventLog.WriteEntry("Odataquery", "" + OdataTable[0][i]);

                    i++;
                }

              //  EventLog.WriteEntry("Odataquery", "" + NoOfRows);
                for (int k = 1; k < NoOfRows; k++)
                {
                    OdataTable[k] = new string[NoOfColoumns];
                    
                }
                int col = 0;
                //EventLog.WriteEntry("Odataquery","no of rows: "+ NoOfRows);
                foreach (string field in splittedfields)
                {

                    string nameField;
                    if (field.Contains("/"))
                    {
                        nameField = field.Replace("/", ".");//field.Split('/')[field.Split('/').Length-1];
                    }
                    else
                    {
                        nameField = field;
                    }

                    //OdataRows =  //OdataFeeds.GetElementsByTagName(("d:" + nameField));
                   // EventLog.WriteEntry("Odataquery", "before filling ..");
                    for (int j = 1; j <= JO.Count; j++)
                    {

                        OdataTable[j][col] = (string)JO[j - 1].SelectToken(nameField);
                        string s = (string)JO[j - 1].SelectToken(nameField);
                        //EventLog.WriteEntry("Odataquery", s);
                    }




                    col++;
                }


                return OdataTable;



            }




            void ProcessCustomReportData(Guid guid, String Query, string odatastr,String ReportName)
            {

                 MemoryStream stream = PopulateChart(Query, odatastr);
                 if (stream != null)
                 {
                     this.sessionData.TrendsChartCache.AddStream(guid, stream, ExportFormat.CSV, ReportName);
                 }

            }

            public string MakeOdataQuery(String ReportName,string SiteId, String Query)
            {
                Query = Query.Replace("amp;","");
                 sessionData = LogOnSessionFactory.Instance.GetCurrent();
                using (new Win32ImpersonationContext(sessionData.Win32Identity))
                {
                    //get the machine name               
                    ICollection<ConnectorAddress> ConnectorAd = sessionData.SitesById[SiteId].GetConnectorAddresses(typeof(IBrokerConnector));                    
                    ConnectorAddress ServernameConnector = ConnectorAd.ElementAt<ConnectorAddress>(0);


                    //construct query
                    string odataqueryString = "http://" + ServernameConnector.ServerName + "/citrix/monitor/odata/v2/data/" + Query +"&$format=json";                    
                    WebClient wc = new WebClient();                      
                    wc.UseDefaultCredentials = true;
                    //if (!EventLog.SourceExists("odataquery"))
                    //    EventLog.CreateEventSource("odataquery", "Application");
                    //try
                    //{
                    //    EventLog.WriteEntry("odataquery", odataqueryString);
                    //}
                    //catch { }
                    //wc.Headers.Add("Accept", "application/xml");
                    string odatastr = wc.DownloadString(odataqueryString);
                    //wc.Dispose();
                    //try
                    //{
                    //    EventLog.WriteEntry("odataquery", odatastr);
                    //}
                    //catch(Exception e) {
                    //    EventLog.WriteEntry("odataquery",e.Message );
                    //}
                    Guid guid = Guid.NewGuid();

                    this.sessionData.TrendsChartCache.AddKey(guid);
                    //create collection
                    //try
                    //{
                    //    EventLog.WriteEntry("odataquery", odatastr);
                    //}
                    //catch { }
                    Task.Factory.StartNew(() => ProcessCustomReportData(guid,Query,odatastr,ReportName));
                              

                    return guid.ToString();
                }
            }




            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                }
            }

        }
    }


