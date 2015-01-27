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

            Console.WriteLine("Done!");
            Console.ReadKey();
        }

        static async Task RunAsync()
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("http://wiki.teamliquid.net/starcraft2/Players_(Europe)");
                HttpResponseMessage response = await client.GetAsync(client.BaseAddress);
                if (response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync();



                    int c = 0;
                    int start = 0;
                    int end = 0;
                    
                    try
                    {
                        
                        while (c < (responseString.Length - 100))
                        {
                            
                            start = responseString.IndexOf("<h3><span class=\"mw-headline\" id=", c);
                            end = responseString.IndexOf("</h3>", start);

                            //This for loop goes through each char in the headline block and does a Console.Write
                            //for every char that isn't nested in brackets (so everything but HTML markup)

                            string headline = InnerText(responseString, start, end);
                            Console.WriteLine(headline);
                            c = end + 4;

                        }
                    }catch(ArgumentOutOfRangeException)
                    {
                        //Console.WriteLine("Index is out of range; responseString.length = " + responseString.Length.ToString() + ", c = " + c.ToString() + ", start = " + start.ToString() + " and end = " + end.ToString());
                        Console.ReadKey();
                    }
                    
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
