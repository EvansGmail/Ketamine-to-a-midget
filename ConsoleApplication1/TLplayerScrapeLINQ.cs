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
                //The "Continent" pages is where I get lists of names for the database to choose which players the user wants to follow
                Uri[] continentPagesURI = new Uri[4];
                continentPagesURI[0] = new Uri("http://wiki.teamliquid.net/starcraft2/Players_(Europe)");
                continentPagesURI[1] = new Uri("http://wiki.teamliquid.net/starcraft2/Players_(US)");
                continentPagesURI[2] = new Uri("http://wiki.teamliquid.net/starcraft2/Players_(Asia)");
                continentPagesURI[3] = new Uri("http://wiki.teamliquid.net/starcraft2/Players_(Korea)");
                client.BaseAddress = continentPagesURI[0];

                foreach (Uri continentUri in continentPagesURI)
                {
                    
                    HttpResponseMessage response = await client.GetAsync(continentUri);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseString = await response.Content.ReadAsStringAsync();
                    
                        int c = 0;
                        int countryStart = 0;
                        int countryEnd = 0;
                        int tableStart = 0;
                        int tableEnd = 0;
                        int tr_candidate = 0;
                        int tr_end = 0;
                        int td_start = 0;
                        int td_end = 0;
                        int td_length = 0;
                        string td_info;
                    
                        try
                        {
                        
                            while (c < (responseString.Length - 40))
                            {
                            
                                countryStart = responseString.IndexOf("<h3><span class=\"mw-headline\" id=", c);
                                if (countryStart == -1)
                                    break;

                                countryEnd = responseString.IndexOf("</h3>", countryStart);

                                //InnerText() goes through each char in the responseString from start to end and does a Console.Write
                                //for every char that isn't nested in brackets (so everything that isn't HTML markup)

                                string countryName = InnerText(responseString, countryStart, countryEnd).Trim();
                                Console.WriteLine(countryName);
                                c = countryEnd + 4;

                                //Find the scope of the current country table

                                tableStart = responseString.IndexOf("<table ", countryEnd);
                                tableEnd = responseString.IndexOf("</table", tableStart);

                                c = tableStart + 6;

                                //Need to add a loop here to account for multiple tables, and how to deal with inactive players/coaches, etc.


                                while (c < tableEnd)
                                {

                                    //Now, I need to look for every <tr bgcolor="(red, blue, green or yellow)"> until I run into the end of the Country; I can start from c because I already incemented it.
                                    //tr_candidate is the location of a tr bgcolor; I need to check to see if it is one of the right colors
                                    //***For now, this code just looks for the first tr bgcolor! I need to make it loop!***

                                    tr_candidate = responseString.IndexOf("<tr bgcolor=", c); //finds a <tr> with a bgcolor specified, which should be a player
                                    
                                    if (tr_candidate == -1) break;
                                    
                                    tr_end = responseString.IndexOf("</tr>", tr_candidate);
                                    string colorCode = responseString.Substring(tr_candidate + 13, 7); //grabs just the 7-character color code

                                    if (colorCode.Equals("#B8B8F2") //blue (Terran)
                                        || colorCode.Equals("#B8F2B8") //green (Protoss)
                                        || colorCode.Equals("#F2B8B8") //pink (Zerg)
                                        || colorCode.Equals("#F2E8B8")) //ugly (Random?)
                                    {
                                        //We've found a player TR! So grab the info out of each <td> (some may be empty!) and spill it
                                        //This would be the time to initialize a player object, and then fill in info as it comes up in the for loop.


                                        for (var i = 1; i <= 6; i++)
                                        {
                                            //There should be exactly 6 TDs; for now cycle through them. If liquipedia changes this, it will break
                                            td_start = nextTDstart(responseString, tr_candidate);
                                            td_end = nextTDend(responseString, tr_candidate);
                                            td_length = nextTDlength(responseString, tr_candidate);

                                            //Do the following operations on just the TD
                                            td_info = responseString.Substring(td_start, td_length);
                                            //Remove the <span> tags that are duplicating information
                                            td_info = removeTag(td_info, "span");
                                            //Clip out all the tags
                                            td_info = InnerText(td_info, 0, td_info.Length).Trim();

                                            Console.WriteLine(removeCharCodes("     " + td_info));
                                            Console.ReadKey();

                                            //move the starting point
                                            tr_candidate = td_end;
                                        }
                                        Console.WriteLine(); //Just adding a line for space here.
                                    }
                                    c = tr_end;
                                }
                                Console.ReadKey();
                            }
                        }catch(ArgumentOutOfRangeException)
                        {
                            Console.WriteLine("Index is out of range; responseString.length = " + responseString.Length.ToString()
                                + ", c = " + c.ToString()
                                + ", start = " + countryStart.ToString()
                                + ", end = " + countryEnd.ToString()
                                + ", tr_candidate = " + tr_candidate.ToString()
                                + ", tr_end = " + tr_end.ToString()
                                + ", td_start = " + td_start.ToString()
                                + ", td_end = " + td_end.ToString());
                            Console.ReadKey();
                        }
                    
                    }
                }
                  
            }
        }

        private static string InnerText(string inputHTML, int start, int end)
        {
            int nesting = 0;
            string nestString = "<";
            string unnestString = ">";
            string innerTextString = "";
            string oneCharacter;
            
            //This is potentially confusing, because I call it "nesting" when it's really just keeping track of brackets,
            // and there should never really be a nested bracket in HTML. I think I could just start capturing after a ">"
            // until I reach a "<" without actually keeping track of the nesting.

            for (int i = start; i < end; i++)
            {
                oneCharacter = inputHTML[i].ToString();
                if (oneCharacter.Equals(nestString))
                {
                    nesting++;
                }
                else if (oneCharacter.Equals(unnestString))
                {
                    nesting--;
                }

                if ((nesting == 0) && !(oneCharacter.Equals("<")) && !(oneCharacter.Equals(">")))
                {
                    innerTextString = innerTextString + oneCharacter;
                }
            }

            innerTextString = innerTextString.Trim();

            if (innerTextString.Length == 0)
            {
                return "No Matching Text found";
            } else
            return innerTextString;
        }
        
        public static string removeCharCodes(string inputString)
        {
            return inputString.Replace("&#160;","");
        }

        public static int nextTDstart(string searchString, int startPosition)
        {
            return searchString.IndexOf("<td", startPosition);
        }

        public static int nextTDend(string searchString, int startPosition)
        {
            return (searchString.IndexOf("</td>", startPosition) + 5);
        }

        public static int nextTDlength(string searchString, int startPosition)
        {
            return (nextTDend(searchString, startPosition) - nextTDstart(searchString, startPosition));
        }

        public static string removeTag(string sourceString, string tagToRemove)
        {
            string startTagString = "<" + tagToRemove;
            string endTagString = "</" + tagToRemove + ">";
            int startTag = sourceString.IndexOf(startTagString);
            int endTag = (sourceString.IndexOf(endTagString) + endTagString.Length);

            if ((startTag != -1) && (endTag != -1) && (startTag < endTag))
            {
                int removeLength = endTag - startTag;
                return sourceString.Remove(startTag, removeLength);
            }
            else return sourceString;
        }

        public class personObject
        {
            //Create a new personObject with all details (but not content) to be scraped from various sources
            public personObject()
            {
            }

            public personObject(
                string liquipediaName,
                string liquipediaURI,
                string bnetName,
                string bnetProfileURI,
                string teamName,
                string teamSiteURI,
                string irlName,
                string twitterName,
                string country,
                string twitterURI,
                string tlName,
                string tlProfileURI,
                string fbName,
                string fbURI,
                string twitchName,
                string twitchURI,
                bool followed
                )
            {
                uniqueID = liquipediaName;
            }

            //A unique identifier for each person; since liquipedia can only have one page per player,
            //and that's what I'm using as a source to scrape potential players, I will use it as the
            //unique ID for now.
            private string uniqueID;
            public string liquipediaName
            {
                get { return uniqueID; }
                set { uniqueID = value; }
            }

            private string liquipediaURIvalue;
            private string liquipediaURI
            {
                get { return liquipediaURIvalue; }
                set { liquipediaURIvalue = value; }
            }

            private string bnetNamevalue;
            private string bnetName
            {
                get { return bnetNamevalue; }
                set { bnetNamevalue = value; }
            }

            private string bnetProfileURIvalue;
            private string bnetProfileURI
            {
                get { return bnetProfileURIvalue; }
                set { bnetProfileURIvalue = value; }
            }

            private string teamNamevalue;
            private string teamName
            {
                get { return teamNamevalue; }
                set { teamNamevalue = value; }
            }

            private string teamSiteURIvalue;
            private string teamSiteURI
            {
                get { return teamSiteURIvalue; }
                set { teamSiteURIvalue = value; }
            }

            private string irlNamevalue;
            private string irlName
            {
                get { return irlNamevalue; }
                set { irlNamevalue = value; }
            }

            private string twitterNamevalue;
            private string twitterName
            {
                get { return twitterNamevalue; }
                set { twitterNamevalue = value; }
            }

            private string countryvalue;
            private string country
            {
                get { return countryvalue; }
                set { countryvalue = value; }
            }

            private string twitterURIvalue;
            private string twitterURI
            {
                get { return twitterURIvalue; }
                set { twitterURIvalue = value; }
            }

            private string tlNamevalue;
            private string tlName
            {
                get { return tlNamevalue; }
                set { tlNamevalue = value; }
            }

            private string tlProfileURIvalue;
            private string tlProfileURI
            {
                get { return tlProfileURIvalue; }
                set { tlProfileURI = value; }
            }

            private string fbNamevalue;
            private string fbName
            {
                get { return fbNamevalue; }
                set { fbNamevalue = value; }
            }

            private string fbURIvalue;
            private string fbURI
            {
                get { return fbURIvalue; }
                set { fbURIvalue = value; }
            }

            private string twitchNamevalue;
            private string twitchName
            {
                get { return twitchNamevalue; }
                set { twitchNamevalue = value; }
            }

            private string twitchURIvalue;
            private string twitchURI
            {
                get { return twitchURIvalue; }
                set { twitchURIvalue = value; }
            }

            private bool followedvalue;
            private bool followed
            {
                get { return followedvalue; }
                set { followedvalue = value; }
            }
        }
    }

}
