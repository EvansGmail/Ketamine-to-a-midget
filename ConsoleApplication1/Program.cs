using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading.Tasks;

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
                // Send Http Requests Here
                client.BaseAddress = new Uri("http://www.drudgereport.com");
                //I'm leaving the default headers in, not the least because the second line below gives an error.
                //client.DefaultRequestHeaders.Accept.Clear();
                //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage response = await client.GetAsync(client.BaseAddress);
                if (response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    int start = responseString.IndexOf("<! MAIN HEADLINE>");
                    int end = responseString.IndexOf("<!-- Main headlines links END --->");
                    int headlineStart = 0;
                    int headlineLen = 0;
                    int nesting = 0;
                    //Console.WriteLine(responseString[start]);
                    string nestString = "<";
                    string unnestString = ">";
                    //This for loop goes through each char in the headline block and does a Console.Write
                    //for every char that isn't nested in brackets (so everything but HTML markup)
                    for (int i = start; i <= end; i++)
                    {
                        if (responseString[i].ToString().Equals(nestString))
                        {
                            //Console.WriteLine("Nesting deeper!");
                            nesting++;
                        } else if (responseString[i].ToString().Equals(unnestString))
                        {
                            //Console.WriteLine("Nesting shallower!");
                            nesting--;
                        }
                        
                        if ((nesting == 0) && !(responseString[i].ToString().Equals("<")) && !(responseString[i].ToString().Equals(">"))) 
                        {
                            if (headlineLen == 0)
                            {   
                                headlineStart = i;
                            }
                            headlineLen++;
                            Console.Write(responseString[i]);
                        }
                    }
                    
                }
            }
        }
    }

}
