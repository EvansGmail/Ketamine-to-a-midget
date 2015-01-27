using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            //Blocking forward progress while I wait for an Http request, because this is just a console app (so no backgrounding)
            RunAsync().Wait();
            
            Console.ReadKey();
        }

        static async Task RunAsync()
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("http://www.drudgereport.com");
                HttpResponseMessage response = await client.GetAsync(client.BaseAddress);
                if (response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    int start = responseString.IndexOf("<! MAIN HEADLINE>");
                    int end = responseString.IndexOf("<!-- Main headlines links END --->",start);
                                        
                    //This for loop goes through each char in the headline block and does a Console.Write
                    //for every char that isn't nested in brackets (so everything but HTML markup)
                    
                    string headline = InnerText(responseString, start, end);
                    Console.WriteLine(headline);
                }
            }
        }

        private static string InnerText(string responseString, int start, int end)
        {
            int nesting = 0;
            string nestString = "<";
            string unnestString = ">";
            string innerTextString = "";
            for (int i = start; i <= end; i++)
            {
                if (responseString[i].ToString().Equals(nestString))
                {
                    nesting++;
                }
                else if (responseString[i].ToString().Equals(unnestString))
                {
                    nesting--;
                }

                if ((nesting == 0) && !(responseString[i].ToString().Equals("<")) && !(responseString[i].ToString().Equals(">")))
                {
                    innerTextString = innerTextString + responseString[i];
                }
            }
            if (innerTextString.Length == 0)
            {
                return "No Matching Text found";
            } else
            return innerTextString;
        }
    }

}
