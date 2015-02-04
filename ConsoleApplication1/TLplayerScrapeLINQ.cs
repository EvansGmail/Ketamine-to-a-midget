using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using HTMLUtils;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            //Blocking forward progress while I wait for the Http requests for player info
            //UTF8 encoding required for Chinese characters (but still won't work in console window)
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            //dayaStuff.myData is the save follow for followed players. It is currently a binary file; a move to XML may make sense later to make it easier to hack/extend.
            string fileName = "dataStuff.myData";
            

            List<personObject> tlPeople = new List<personObject>();
            //Not actually saving/serializing these now; just for caching during a single use
            List<tlCachedPostPage> cachedPostPages = new List<tlCachedPostPage>();

            //Most of the functionality is hidden in RunAsync right now. It grabs all the web data.
            ScrapeGlobalPlayerLists(tlPeople).Wait();
            
            //Default ordering is by Liquipedia Name
            tlPeople = tlPeople.OrderBy(o => o.liquipediaName).ToList();

            Console.WriteLine("Done! " + tlPeople.Count.ToString() + " players found!");

            DeserializeFollowedPlayers(fileName, tlPeople); //Take players from "filename" and put them into List tlPeople (or create fileName if it doesn't exist)
            
            int quitThisGame = 0;

            while (quitThisGame == 0)
            {
                Console.WriteLine("Choose one of the following options: \n" +
                                        "A. Sort list by some property \n" +
                                        "B. Follow a user \n" +
                                        "C. Unfollow a user \n" +
                                        "D. Print the list, as sorted \n" +
                                        "E. Print the followed user list \n" +
                                        "F. Print player detail \n" +
                                        "G. Print player TL posts Summary \n" +
                                        "Q. Quit");

                Console.WriteLine();
                string inKey = Console.ReadLine().ToUpper();
                Console.WriteLine();
                switch (inKey)
                {
                    case "A":
                        Console.WriteLine("Choose a property to sort the list by: \n" +
                                        "A. Liquipedia Name \n" +
                                        "B. Real Name \n" +
                                        "C. Team Name \n" +
                                        "D. Country \n" +
                                        "E. Main Race \n" +
                                        "Any other key to quit.");
                        Console.WriteLine();
                        string inKey2 = Console.ReadLine().ToUpper();
                        Console.WriteLine();
                        switch (inKey2)
	                        {
                            case "A":
                                    tlPeople = tlPeople.OrderBy(o => o.liquipediaName).ToList();
                                    break;
                            case "B":
                                    tlPeople = tlPeople.OrderBy(o => o.irlName).ToList();
                                    break;
                            case "C":
                                    tlPeople = tlPeople.OrderBy(o => o.teamName).ToList();
                                    break;
                            case "D":
                                    tlPeople = tlPeople.OrderBy(o => o.country).ToList();
                                    break;
                            case "E":
                                    tlPeople = tlPeople.OrderBy(o => o.mainRace).ToList();
                                    break;
                            default:
                                    break;
	                        }
                        break;
                    case "B":
                        Console.WriteLine("Type the Liquipedia Name of the person to follow:");
                        Console.WriteLine();
                        string personToFollow = Console.ReadLine().ToUpper();
                        followAndSerialize(personToFollow, tlPeople, fileName);
                        break;
                    case "C":
                        Console.WriteLine("Type the Liquipedia Name of the person to unfollow:");
                        Console.WriteLine();
                        string personToUnfollow = Console.ReadLine().ToUpper();
                        unfollowAndStopSerializing(personToUnfollow, tlPeople, fileName);
                        break;
                    case "D":
                        foreach (personObject person in tlPeople)
                        {
                            person.displayPersonProperties();
                        }
                        break;
                    case "E":
                        var listOfFollowed = (from v in tlPeople
                                              where v.followed
                                              select v);
                        foreach (personObject person in listOfFollowed)
                        {
                            person.displayPersonProperties();
                        }
                        break;
                    case "F":
                        Console.WriteLine("Type the Liquipedia Name of the person whose details you want to see:");
                        Console.WriteLine();
                        string personForDetailView = Console.ReadLine().ToUpper();
                        extractPersonDetail(personForDetailView, tlPeople).Wait();
                        break;
                    case "G":
                        Console.WriteLine("Type the name of the person whose TL post details you want to see:");
                        Console.WriteLine();
                        string tlForumNameForPosts = Console.ReadLine().ToUpper();
                        personObject personForPosts = personObjectFromString(tlForumNameForPosts, tlPeople);
                        HttpClient client2 = new HttpClient();
                        grabTlPosts(personForPosts, client2, cachedPostPages, 10, tlPeople).Wait();
                        break;
                    case "Q":
                        quitThisGame = 1;
                        break;
                    default:
                        break;
                }
                Console.WriteLine();
            }


        }

        static personObject personObjectFromString(string personString, List<personObject> tlPeople)
        {
            var person = (from u in tlPeople
                          where u.liquipediaName.ToUpper() == personString.ToUpper()
                          select u);
            if (person.FirstOrDefault() == null)
            {
                Console.WriteLine("Person not found!");
                return null;
            }
            else
            {
                return person.FirstOrDefault();
            }
        }

        static async Task extractPersonDetail(string personForDetailView, List<personObject> tlPeople)
        {
            personObject person = personObjectFromString(personForDetailView, tlPeople);

            //1. Load async the players teamliquid.net profile URL
            using (var client = new HttpClient())
            {
                Uri playerDetailUri = new Uri(person.liquipediaURI.ToString());
                    
                var response = await client.GetAsync(playerDetailUri);

                if (response.IsSuccessStatusCode)
                {
                    UTF8Encoding utf8 = new UTF8Encoding();
                    //string responseString = utf8.GetString(response);
                    string responseString = await response.Content.ReadAsStringAsync();

                    string infoBox_tags = HTMLUtilities.StringFromTag(responseString, "<div class=\"infobox-center infobox-icons\">", "</div>");

                    //2. Scrape that page for the rest of the detail properties
                    //3. Fill those (switch like the main list scraper)

                    person.tlForumURI = HTMLUtilities.hrefUriFromTitle(infoBox_tags, "TeamLiquid.net Profile");
                    if (person.tlForumURI != null) person.tlName = tlNameFromURI(person.tlForumURI);
                    person.twitterURI = HTMLUtilities.hrefUriFromTitle(infoBox_tags, "Twitter");
                    if (person.twitterURI != null) person.twitterName = twitterNameFromURI(person.twitterURI);
                    person.fbURI = HTMLUtilities.hrefUriFromTitle(infoBox_tags, "Facebook");
                    if (person.fbURI != null) person.fbName = fbNameFromURI(person.fbURI);
                    person.twitchURI = HTMLUtilities.hrefUriFromTitle(infoBox_tags, "Twitch Stream");
                    if (person.twitchURI != null) person.twitchName = twitchIDfromURI(person.twitchURI);
                    person.redditProfileURI = HTMLUtilities.hrefUriFromTitle(infoBox_tags, "Reddit Profile");
                    if (person.redditProfileURI != null) person.redditUsername = redditNameFromURI(person.redditProfileURI);

                    //There are some other tags, e.g., battle.net urls, that show up later under "external links"
                    //Since I really want to move on to pulling the posts from TL, I'm putting off grabbing that
                    //stuff until later.

                    //If a Tl.net profile URI exists, scrape that page for information (how often posts, total posts (I think I should serialize this), etc.)
                    if (person.tlForumURI != null)
                    {
                        string tlProfilePageString = await HTMLUtilities.getHTMLStringFromUriAsync(client, person.tlForumURI);
                        string numPostsTags = HTMLUtilities.StringFromTag(tlProfilePageString, "<a href='search.php?q=&amp;t=c&amp;f=-1&u=", "</a>");
                        string numPosts = HTMLUtilities.InnerText(numPostsTags, 0, numPostsTags.Length);
                        person.tlTotalPosts = Convert.ToInt32(numPosts);
                    }
                }    
            }
            //4. Display the details (will ultimately return)
            if (person.tlName != null) Console.WriteLine(person.tlName + " on teamliquid: " + person.tlForumURI);
            if (person.twitterName != null) Console.WriteLine(person.twitterName + " on Twitter: " + person.twitterURI);
            if (person.fbName != null) Console.WriteLine(person.fbName + " on Facebook: " + person.fbURI);
            if (person.twitchName != null) Console.WriteLine(person.twitchName + " on Twitch.tv: " + person.twitchURI); //updates the one scraped from the countries list, for uniformity
            if (person.redditUsername != null) Console.WriteLine(person.redditUsername + " on Reddit: " + person.redditProfileURI);
            if (person.tlTotalPosts != 0) Console.WriteLine("Total posts on TeamLiquid.net: " + person.tlTotalPosts);
        }

        static async Task grabTlPosts(personObject person, HttpClient client, List<tlCachedPostPage> cachedPostPages, int postsToGrab, List<personObject> tlPeople)
        {
            Uri postsPage = tlPostUriFromTlUsername(person.tlName);
            string tlPostsResultPage = await HTMLUtilities.getHTMLStringFromUriAsync(client, postsPage);
            string postsBlock = HTMLUtilities.StringFromTag(tlPostsResultPage, "<tr><td class='srch_res1'>", "</td></tr></TABLE>");
            int readPosition = 0;
            int threadCount = 0;
            int postCount = 0;
            string srch_res_toggle = "1";
            person.tlPostList = new List<tlPostObject>();
            string commentText = "";

            while (readPosition != -1 && readPosition < postsBlock.Length && postCount < postsToGrab)
            {
                //Have to read through by TDs, but add list items by individual links
                //So, keep track of post general information by TD, then add it all at each link
                //(So there will be a while loop in this while loop)
                int threadBlock_start = postsBlock.IndexOf("<tr><td class='srch_res", readPosition);
                int threadBlock_end = postsBlock.IndexOf("</td></tr>", threadBlock_start) + "</td></tr>".Length;
                readPosition = threadBlock_start;
                string threadBlock = postsBlock.Substring(threadBlock_start, threadBlock_end - threadBlock_start);
                string post_forum_block = HTMLUtilities.StringFromTag(threadBlock, "<td class='srch_res" + srch_res_toggle + "'><font size='-2' color='#808080'>", "</font>");
                string post_forum = HTMLUtilities.InnerText(post_forum_block, 0, post_forum_block.Length).TrimEnd(":".ToCharArray());
                string thread_title_block = HTMLUtilities.StringFromTag(threadBlock, "<a class='sl' name='srl' href=", "</a>");
                string thread_title = HTMLUtilities.InnerText(thread_title_block, 0, thread_title_block.Length);
                string thread_Uri_stub = "http://www.teamliquid.net" + HTMLUtilities.grabHREF(thread_title_block);
                Console.WriteLine(thread_title);
                Console.WriteLine();

                //Process thread posts here
                int post_list_block_start = threadBlock.IndexOf("<a class='sls' name='srl' href='viewpost.php?post_id=");
                int post_list_block_end = threadBlock.IndexOf("</td>", post_list_block_start);
                int post_list_block_length = post_list_block_end - post_list_block_start;
                string post_list_block = threadBlock.Substring(post_list_block_start, post_list_block_length);
                int subThread_position = 0;

                while (subThread_position != -1 && postCount < postsToGrab)
                {
                    //This is NOT DUPLICATING the block immediately before; it handles situations where there are more than
                    //one comment per thread.
                    int postLink_start = post_list_block.IndexOf("<a class='sls' name='srl' href='viewpost.php?post_id=", subThread_position);
                    int postLink_end = post_list_block.IndexOf("</a>", postLink_start) + "</a>".Length;
                    int postLink_length = postLink_end - postLink_start;
                    string postLink_tags = post_list_block.Substring(postLink_start, postLink_length);
                    Uri postLink = new Uri("http://www.teamliquid.net/forum/" + HTMLUtilities.grabHREF(postLink_tags));
                    int postNumber = Convert.ToInt32(HTMLUtilities.InnerText(postLink_tags, 0, postLink_tags.Length));
                    subThread_position = post_list_block.IndexOf("<a class='sls' name='srl' href='viewpost.php?post_id=", postLink_end);
                    
                    //I think I should use a separate client for each comment. That way, in the future, I can grab the pages concurrently
                    //in separate threads. For now, I'm waiting in between.
                    
                    //Check to see if the post page has already been cached; you should have added a property to the tlPostObject if it has been
                    tlCachedPostPage cachedPage = getCachedPage(cachedPostPages, thread_Uri_stub, postNumber);

                    //If the page is ripe for a refresh, do it (shouldn't really return anything)
                    
                    //This is now a bit more complicated... need to scrape the page, add postObjects to the cachedPageObject's list of posts
                    //Since detecting edits/deletions would require comparing individual text, it probably just makes more sense to overwrite
                    //any existing postObjects for that page. As for the player... you shouldn't need to check. Their list references the same objects
                    //as the cachedPageObject's list, so as long as you're updating the object information (and not replacing with new objects,)
                    //it should just work.
                    if (cachedPage.needsRefresh)
                    {
                        HttpClient commentClient = new HttpClient();
                        
                        commentText = await grabThreadPageHTMLAsync(cachedPage, commentClient, postLink, postNumber, tlPeople);
                        //...just pass the cache page (which has to exist at this point) and have it update values right in the method
                        //This is fine because literally every time you pull the HttpRequest, you should update the object cache
                    }
                    else
                    {
                        //If it doesn't need a refresh... grab the text from the existing post object
                        commentText = cachedPage.posts.Where(s => s.commentUri.ToString() == thread_Uri_stub).FirstOrDefault().postContent;
                    }
                    
                    //Spit out a cached version of the post
                    Console.WriteLine("Comment # " + postNumber + ":");
                    Console.WriteLine();
                    Console.WriteLine(commentText);
                    Console.WriteLine();
                    postCount++;
                }

                Console.WriteLine("-----");
                readPosition += threadBlock.Length;

                if (srch_res_toggle == "1")
                    {
                        srch_res_toggle = "2";
                    }
                    else
                    {
                        srch_res_toggle = "1";
                    }
                threadCount++;
            }
            return;
        }

        private static tlCachedPostPage getCachedPage(List<tlCachedPostPage> cachedPostPages, string thread_Uri_stub, int postNumber) //You should overload this to take a post object
        {
            var CachedPagesThisThread = (from u in cachedPostPages
                                         where u.cachedPageRemoteUri == new Uri(thread_Uri_stub) //postLink is wrong... should be thread page link (maybe use the UniqueID)
                                         select u);

            //Items.Where( i => i.TestList.All(li => li.State == 2))
            var match = CachedPagesThisThread.Where(q => q.posts.All(li => li.commentUri.ToString() == thread_Uri_stub)).FirstOrDefault();

            //Did you find a matching post result?
            if (match == null)
            {
                //  There is no cache page yet. Create a cachePage object for it,
                tlCachedPostPage cachedPageObject = new tlCachedPostPage();
                cachedPageObject.cachedPageRemoteUri = new Uri(thread_Uri_stub);
                cachedPageObject.needsRefresh = true;

                //tlCachedPostPage cachedPageObject = new tlCachedPostPage(null,  //not creating or referencing a file yet
                //                                                         new Uri(thread_Uri_stub),
                //                                                         true, //This means the client needs to grab the posts. It always starts true
                //                                                         null); //The list of posts. Will add when I refresh it, below
                
                Console.WriteLine("Cachepageobject.needsRefresh = " + cachedPageObject.needsRefresh.ToString());

                //  ...add it to the list of cached pages, and...
                cachedPostPages.Add(cachedPageObject);
                return cachedPageObject;
                //  ...add the posts on the page to it (do this later, so that you don't have to call it twice) 
            }
            else
            {
                //  There is a cache page for it! Check to see if it is ripe for a refresh.
                tlCachedPostPage cachedPageObject = (tlCachedPostPage)match;
                return cachedPageObject;
            }    
        }

        static Uri tlPostUriFromTlUsername(string tlUsername)
        {
            return new Uri("http://www.teamliquid.net/forum/search.php?q=&t=c&f=-1&u=" + tlUsername + "&gb=date&d=");
        }

        private static async Task<string> grabThreadPageHTMLAsync(tlCachedPostPage cachedPage, HttpClient client, Uri postLink, int postNumber, List<personObject> tlPeople)
        {
            string threadPage = await HTMLUtilities.getHTMLStringFromUriAsync(client, postLink);
            string returnString = null;

            //Scrape the unique thread ID
            string threadLinkBlock = HTMLUtilities.StringFromTag(threadPage, "<link rel=\"canonical\"", "</head>");
            string postLinkString = HTMLUtilities.grabHREF(threadLinkBlock);
            int thread_id_start = postLinkString.LastIndexOf("/") + 1;
            int thread_id_length = postLinkString.IndexOf("-", thread_id_start) - thread_id_start;
            int thread_id = Convert.ToInt32(postLinkString.Substring(thread_id_start, thread_id_length));
            
            //Scrape the thread title - this includes the page number, which sucks, but isn't a priority
            string thread_title = HTMLUtilities.InnerText(HTMLUtilities.StringFromTag(threadPage, "<title>", "</title>"));

            //Scrape the post forum
            string subforumBlock = HTMLUtilities.StringFromTag(threadPage, "Forum Index</span></a></span>", "</td>");
            string subforumStartString = "<span itemprop=\"title\">";
            int subforumStart = subforumBlock.LastIndexOf(subforumStartString) + subforumStartString.Length;
            string subForumEndString = "</span></a></span>";
            int subforumLength = subforumBlock.IndexOf(subForumEndString, subforumStart) - subforumStart;
            string postSubforum = subforumBlock.Substring(subforumStart, subforumLength);

            //Scrape the individual posts!
            int readPosition = 0;
            string startBlock = "<tr><td colspan=\"2\"><a name=\"";
            string endBlock = "</table><br></td></tr>";
            
            while (readPosition != -1)
            {
                string commentBlock = HTMLUtilities.StringFromTag(threadPage, startBlock, endBlock, readPosition);
                //Scrape the post blocks here
                
                //Scrape the post link
                string singlePostLink = "http://www.teamliquid.net" +
                                        HTMLUtilities.grabHREF(
                                        HTMLUtilities.StringFromTag(commentBlock, "<a href=\"/forum/viewpost.php?post_id\"", " class=\"submessage\""));

                //Scrape the post Number
                int singlePostNumber = Convert.ToInt32(
                                            HTMLUtilities.StringFromParameter(
                                                HTMLUtilities.StringFromTag(commentBlock, "<a name=\"", "</a>"),
                                                "name")
                                            );

                //Scrape the post Author
                string authorBlock = HTMLUtilities.StringFromTag(commentBlock, "<span class='forummsginfo'>&nbsp;", "</span>");
                int authorName_start = authorBlock.IndexOf("&nbsp;") + 6;
                int authorName_length = authorBlock.LastIndexOf(" &nbsp;") - 1 - authorName_start;
                string authorName = authorBlock.Substring(authorName_start, authorName_length);

                //Scrape the comment text
                //That StringFromTag could cause a problem if someone's post contains the HTML </table><br></td></tr>, which I assume is possible
                //Quotes themselves don't seem to break it, which is great (because that would be really common) but if someone uses a table
                //in their post, it will probably break
                string commentTags = HTMLUtilities.StringFromTag(commentBlock, "<td class='forumPost'", "</td></tr></table>"); //Same potential problem as above
                string commentHTML = HTMLUtilities.InnerText(commentTags, 0, commentTags.Length);
                
                //Scrape the date/time [TO DO]

                //Impement updating the postObjects. Pseudocode:
                //
                //              if cachedPage.postList contains the post
                //                  update the post info
                //                  you're done
                //              else
                //                  cachedPage.postList.Add(the post)
                //                  if the person is a personObject
                //                      person.postList.Add(the post)
                //              endif

                var matchingPost = (from w in cachedPage.posts
                                   where w.commentNumber == singlePostNumber
                                   select w);
                tlPostObject tempPostObject = matchingPost.FirstOrDefault();

                if (tempPostObject != null)
                {
                    tempPostObject.postContent = commentHTML;
                }
                else
                {
                    cachedPage.posts.Add(tempPostObject);
                    tempPostObject.commentNumber = singlePostNumber;
                    tempPostObject.commentUri = new Uri(singlePostLink);
                    tempPostObject.postContent = commentHTML;
                    tempPostObject.threadSection = postSubforum;
                    tempPostObject.threadTitle = thread_title;
                    tempPostObject.uniqueThreadId = thread_id; //YOU HAVE TO ACTUALLY IMPLEMENT THIS I THINK MAYBE
                
                    if (singlePostNumber == postNumber)
                    {
                        returnString = commentHTML;
                    }

                    var personMatch = (from x in tlPeople
                                       where x.tlName == authorName
                                       select x);
                    personObject tempPerson = personMatch.FirstOrDefault();
                    
                    if (tempPerson != null)
                    {
                        tempPerson.tlPostList.Add(tempPostObject);
                    }
                }

                readPosition = threadPage.IndexOf(startBlock, readPosition + commentBlock.Length);
            }
            return returnString;
        }

        static string twitterNameFromURI(Uri twitterURI)
        {
            return HTMLUtilities.NameFromURI("Twiter Profile", "twitter.com/", twitterURI);
        }

        static string tlNameFromURI(Uri tlProfileURI)
        {
            return HTMLUtilities.NameFromURI("Teamliquid Profile", "teamliquid.net/forum/profile.php?user=", tlProfileURI);
        }

        static string fbNameFromURI(Uri fbProfileURI)
        {
            return HTMLUtilities.NameFromURI("Facebook Profile", "facebook.com/", fbProfileURI);
        }

        static string redditNameFromURI(Uri redditProfileURI)
        {
            return HTMLUtilities.NameFromURI("Reddit Profile", "reddit.com/user/", redditProfileURI);
        }

        private static void unfollowAndStopSerializing(string personToUnfollow, List<personObject> tlPeople, string fileName)
        {
            var personToUnfollowObj = (from u in tlPeople
                                       where u.liquipediaName.ToUpper() == personToUnfollow
                                       select u);
            if (personToUnfollowObj.FirstOrDefault().Equals(null))
            {
                Console.WriteLine("Person not found!");
                return;
            }
            else
            {
                //Check to see if person already not being followed
                if (!personToUnfollowObj.FirstOrDefault().followed)
                {
                    Console.WriteLine("You're not even following " + personToUnfollowObj.FirstOrDefault().liquipediaName + "!");
                }
                else
                {
                    personToUnfollowObj.FirstOrDefault().followed = false;
                    FileStream s = new FileStream(fileName, FileMode.Open);
                    IFormatter formatter = new BinaryFormatter();
                    while (s.Position != s.Length)
                    {
                        long objStartPosition = s.Position;
                        personObject v = (personObject)formatter.Deserialize(s);

                        if (v.liquipediaName == personToUnfollowObj.FirstOrDefault().liquipediaName)
                        {
                            long nextObjPosition = s.Position;
                            //Need to remove data from objStartPosition to (s.Position - 1). So, copy everything from s.Position to the end, and move is to objStartPosition, then truncate
                            long bytesToGrab = s.Length - s.Position;
                            int[] bytesLeft = new int[bytesToGrab];
                            while (s.Position != s.Length)
                            {
                                bytesLeft[s.Position - nextObjPosition] = s.ReadByte();
                            }

                            BinaryWriter bw = new BinaryWriter(s);
                            bw.Seek((int)objStartPosition, SeekOrigin.Begin);
                            for (int i = 0; i < bytesToGrab; i++)
                            {
                                bw.Write((byte)bytesLeft[i]);
                            }
                            s.SetLength(s.Position);
                            //Set the position equal to the end after you truncate the file; that way this while loop will exit
                        }

                    }
                    s.Close();
                    Console.WriteLine("Successfully unfollowed " + personToUnfollowObj.First().liquipediaName);
                }
                return;
            }
        }

        private static void followAndSerialize(string personToFollow, List<personObject> tlPeople, string fileName)
        {
            var personToFollowObj = (from u in tlPeople
                                     where u.liquipediaName.ToUpper() == personToFollow
                                     select u);
            if (personToFollowObj.Count() != 1)
            {
                Console.WriteLine("Person not found!");
                return;
            }
            else
            {
                //Check to see if already followed
                if (personToFollowObj.FirstOrDefault().followed)
                {
                    Console.WriteLine("You're already following " + personToFollowObj.FirstOrDefault().liquipediaName + "!");
                }
                else
                {
                    personToFollowObj.FirstOrDefault().followed = true;
                    FileStream s = new FileStream(fileName, FileMode.Append);
                    IFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(s, personToFollowObj.FirstOrDefault());
                    s.Close();
                    Console.WriteLine("Successfully followed " + personToFollowObj.First().liquipediaName);
                }
                return;
            }
        }

        private static void DeserializeFollowedPlayers(string fileName, List<personObject> tlPeople)
        {
            if (File.Exists(fileName))
            {
                FileStream d = new FileStream(fileName, FileMode.Open);
                IFormatter formatter = new BinaryFormatter();
                if (d.Length != 0)
                {
                    while (d.Position != d.Length)
                    {
                        personObject t = (personObject)formatter.Deserialize(d);

                        var personToFollowObj = (from u in tlPeople
                                                 where u.liquipediaName.ToUpper() == t.liquipediaName.ToUpper()
                                                 select u);
                        if (personToFollowObj.Count() != 1)
                        {
                            Console.WriteLine("Person not found!");
                        }
                        else
                        {
                            personToFollowObj.FirstOrDefault().followed = true;
                            Console.WriteLine("Successfully followed " + personToFollowObj.First().liquipediaName);
                        }
                    }
                }
                d.Close();
            }
            else
            {
                FileStream d = new FileStream(fileName, FileMode.Create);
                d.Close();
            }
        }

        static async Task ScrapeGlobalPlayerLists(List<personObject> tlPeople)
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
                                      
                    var response = await client.GetAsync(continentUri);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        UTF8Encoding utf8 = new UTF8Encoding();
                        //string responseString = utf8.GetString(response);
                        string responseString = await response.Content.ReadAsStringAsync();
                        //var responseString = Encoding.UTF8.GetString(responseStringFromBytes, 0, response.Length - 1);
                        //These two commented rows are relics of my failed attempt at handling foreign-language characters
                        //encoded as UTF8. Apparently, the entire struggle was doomed because my console can't display UTF8
                        //characters with byte lengths longer than 8, even with the encoding set to UTF8.
                        //If I want to ensure UTF8 characters later (after adding a GUI,) I can ressurect these; I mey need to change the
                        //two operative lines to read from a Byte Array and convert it to a String using UTF8 encoding.
                        
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
                        string td_tags = "";
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
                                //No need to actually do the commented out line for country, as each Player comes with a country <TD> tag
                                //string countryName = InnerText(responseString, countryStart, countryEnd).Trim();
                                c = countryEnd + 4;

                                //Find the scope of the current country's table of players

                                tableStart = responseString.IndexOf("<table ", countryEnd);
                                tableEnd = responseString.IndexOf("<h3><span class=\"mw-headline\" id=", tableStart);

                                if (tableEnd == -1) tableEnd = responseString.Length; 

                                c = tableStart + 6;


                                //As it turns out, each country has more than one table (for e.g. retired players, casters, etc.,) so I changed
                                //"tableEnd" to the beginning of the next country; that way it just keeps reading <tr>s until it hits the next
                                //full country block of folks.
                                //I suppose I could skip the whole process and just read ever <tr> on the page at once... but that does make me
                                //nervous. This way I can control the stream a bit more if need be. For example, later I may want to keep track
                                //of which players are active, which are casters, which are retired, etc.

                                while (c < tableEnd)
                                {

                                    //Now, I need to look for every <tr bgcolor="(red, blue, green or yellow)"> until I run into the end of the Country; I can start from c because I already incemented it.
                                    //tr_candidate is the location of a tr bgcolor; I need to check to see if it is one of the right colors
                                    //Why the tr bgcolor? I'm glad you asked! Liquipedia colors the player table rows based on a player's race (Terran,
                                    //Zerg or Protoss,) so it's an easy way to discern whether a row is a player, or a header, or a bunch of blanks.
                                    //It will break if TL ever redesigns these pages, but then... what wouldn't break?
                                    
                                    tr_candidate = responseString.IndexOf("<tr bgcolor=", c); //finds a <tr> with a bgcolor specified, which should be a player
                                    
                                    if (tr_candidate == -1)
                                    {
                                        //Console.WriteLine("No TR tag found");
                                        break;
                                    }else if (tr_candidate > tableEnd)
                                    {
                                        //Console.WriteLine("The next TR tag suprasses this table.");
                                        break;
                                    }
                                                                        
                                    tr_end = responseString.IndexOf("</tr>", tr_candidate);
                                    string colorCode = responseString.Substring(tr_candidate + 13, 7); //grabs just the 7-character color code

                                    if (colorCode.Equals("#B8B8F2") //blue (Terran)
                                        || colorCode.Equals("#B8F2B8") //green (Protoss)
                                        || colorCode.Equals("#F2B8B8") //pink (Zerg)
                                        || colorCode.Equals("#F2E8B8")) //ugly tan color (Random?)
                                    {
                                        //We've found a player TR! So grab the info out of each <td> (some may be empty!) and spill it to the player database
                                        //Creating a new person to put information into
                                        personObject tempPerson = new personObject();

                                        for (var i = 1; i <= 6; i++)
                                        {
                                            //There should be exactly 6 TDs; for now, cycle through them and use switch to assign data to properties.
                                            //If liquipedia changes the table, this (and everything else) will break
                                            td_start = HTMLUtilities.nextTDstart(responseString, tr_candidate);
                                            td_end = HTMLUtilities.nextTDend(responseString, tr_candidate);
                                            td_length = HTMLUtilities.nextTDlength(responseString, tr_candidate);

                                            //Td_tags is just the HTML code for this player; it is easier to inspect with WriteLine than the whole page 
                                            td_tags = responseString.Substring(td_start, td_length);
                                            //Remove the <span>...</span> sections that are duplicating some information (like team names)
                                            td_info = HTMLUtilities.removeTag(td_tags, "span");
                                            //Clip out all the HTML tag <...> substrings; leave just the content 
                                            td_info = HTMLUtilities.InnerText(td_info, 0, td_info.Length).Trim();
                                            //Remove weird character codes like &#160;
                                            td_info = HTMLUtilities.removeCharCodes(td_info);

                                            //Assign the properties you are grabbing to the personObject
                                            switch (i)
                                            {
                                                case 1:
                                                    //tempPerson.liquipediaName = td_info;
                                                    tempPerson.liquipediaName = HTMLUtilities.StringFromParameter(td_tags, "title");
                                                    tempPerson.liquipediaURI = new Uri("http://wiki.teamliquid.net" + HTMLUtilities.grabHREF(td_tags));
                                                    break;
                                                case 2:
                                                    tempPerson.irlName = td_info;
                                                    break;
                                                case 3:
                                                    tempPerson.teamName = td_info;
                                                    break;
                                                case 4:
                                                    tempPerson.country = td_info;
                                                    break;
                                                case 5:
                                                    tempPerson.mainRace = td_info;
                                                    break;
                                                case 6:
                                                    //This will grab twitch IDs, but will need to grab own3d IDs or, e.g. day9.tv
                                                    string hrefTag = HTMLUtilities.grabHREF(td_tags);
                                                    if (hrefTag != null)
                                                    {
                                                        //Trimming slashes and octothorps, because e.g. MarineKing/ and beastyqt#/
                                                        char[] trimChars = { '/', '#' };
                                                        tempPerson.twitchName = twitchIDfromURI(new Uri(hrefTag)).TrimEnd(trimChars);
                                                    }
                                                    else
                                                    {
                                                        tempPerson.twitchName = null;
                                                    }
                                                    break;
                                                default:
                                                    //Console.WriteLine("Oh Gawd. Something has gone horribly wrong. i = " + i.ToString());
                                                    // (It really has, code execution should never reach this)
                                                    //Console.ReadKey();
                                                    break;
                                            }
                                            //move the starting point to look for a new <tr> to the end of the last <td>
                                            tr_candidate = td_end;
                                        }
                                        //Write this tempPerson to the playerObject list
                                        tlPeople.Add(tempPerson);
                                    }
                                    //Move the starting point to look for a new table to the last <tr> end
                                    c = tr_end;
                                }
                            }
                        }catch(ArgumentOutOfRangeException)
                        {
                            Console.WriteLine("An index was out of range; responseString.length = " + responseString.Length.ToString()
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

        public static string twitchIDfromURI(Uri sourceUri)
        {
            string sourceUriString = sourceUri.ToString();
            int idStart = sourceUriString.IndexOf("twitch.tv/") + 10;
            int idEnd = new int();
            
            if (idStart != 9)
            {
                idEnd = sourceUriString.Length;
            }
            else idEnd = -1;

            int idLength = idEnd - idStart;

            if ((idStart != 9) && (idEnd != -1) && (idStart < idEnd))
            {
                return sourceUriString.Substring(idStart, idLength);
            }
            else return "No Twitch ID found";       
        }

        [Serializable()]
        public class personObject : ISerializable
        {
            //Create a new personObject with all details (but not content) to be scraped from various sources
            public personObject()
            {
                //Empty constructor required to compile.
            }

            /// <summary>
            /// Creates an object reference to a person who can be followed on various accounts
            /// </summary>
            /// <param name="liquipediaName">The person's unique Liquipedia page name, if one exists</param>
            /// <param name="liquipediaURI">A Uri object to the person's Liquipedia page, if one exists</param>
            /// <param name="country">The person's home country</param>
            /// <param name="bnetName">The person's battle.net user ID and character code</param>
            /// <param name="bnetProfileURI">The person's battle.net profile Uri</param>
            /// <param name="mainRace">The person's main race played: Terran, Zerg, Protoss or Random</param>
            /// <param name="teamName">The person's team name</param>
            /// <param name="teamSiteURI">A Uri object to the person's team's webpage</param>
            /// <param name="irlName">The person's real name in format First [Middle] Last</param>
            /// <param name="twitterName">The person's Twitter account name</param>
            /// <param name="twitterURI">A Uri object to the person's Twitter profile page</param>
            /// <param name="tlName">The person's TeamLiquid.net/forum username</param>
            /// <param name="tlProfileURI">A Uri object to the person's TeamLiquid.net/forum profile</param>
            /// <param name="tlTotalPosts">The total number of comments the person has posted to TeamLiquid.net</param>
            /// <param name="fbName">The person's Facebook Username</param>
            /// <param name="fbURI">A Uri object to the person's Facebook profile</param>
            /// <param name="twitchName">The person's Twitch.tv Username</param>
            /// <param name="twitchURI">A Uri object to the person's Twitch.tv channel</param>
            /// <param name="followed">True if the user is following this player, false if the user is not</param>
            /// <param name="tlPostList">An object reference to a list of tlPostObjects containing posts made by the person</param>
            public personObject(string liquipediaName,
                                Uri liquipediaURI,
                                string country,
                                string bnetName,
                                Uri bnetProfileURI,
                                string mainRace,
                                string teamName,
                                Uri teamSiteURI,
                                string irlName,
                                string twitterName,
                                Uri twitterURI,
                                string tlName,
                                Uri tlProfileURI,
                                int tlTotalPosts,
                                string fbName,
                                Uri fbURI,
                                string twitchName,
                                Uri twitchURI,
                                bool followed,
                                List<tlPostObject> tlPostList)
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
                set { uniqueID = value;}
            }
            
            private Uri liquipediaURIvalue;
            public Uri liquipediaURI
            {
                get { return liquipediaURIvalue; }
                set { liquipediaURIvalue = value; }
            }

            private Uri tlForumURIvalue;
            public Uri tlForumURI
            {
                get { return tlForumURIvalue;}
                set { tlForumURIvalue = value;}
            }

            private string bnetNamevalue;
            public string bnetName
            {
                get { return bnetNamevalue; }
                set { bnetNamevalue = value; }
            }

            private Uri bnetProfileURIvalue;
            public Uri bnetProfileURI
            {
                get { return bnetProfileURIvalue; }
                set { bnetProfileURIvalue = value; }
            }

            private string mainRaceValue;
            public string mainRace
            {
                get { return mainRaceValue; }
                set { mainRaceValue = value; }
            }

            private string teamNamevalue;
            public string teamName
            {
                get { return teamNamevalue; }
                set { teamNamevalue = value; }
            }

            private Uri teamSiteURIvalue;
            public Uri teamSiteURI
            {
                get { return teamSiteURIvalue; }
                set { teamSiteURIvalue = value; }
            }

            private string irlNamevalue;
            public string irlName
            {
                get { return irlNamevalue; }
                set { irlNamevalue = value; }
            }

            private string twitterNamevalue;
            public string twitterName
            {
                get { return twitterNamevalue; }
                set { twitterNamevalue = value; }
            }

            private string countryvalue;
            public string country
            {
                get { return countryvalue; }
                set { countryvalue = value; }
            }

            private Uri twitterURIvalue;
            public Uri twitterURI
            {
                get { return twitterURIvalue; }
                set { twitterURIvalue = value; }
            }

            private string tlNamevalue;
            public string tlName
            {
                get { return tlNamevalue; }
                set { tlNamevalue = value; }
            }

            private int tlTotalPostsValue;
            public int tlTotalPosts
            {
                get { return tlTotalPostsValue; }
                set { tlTotalPostsValue = value; }
            }

            private Uri redditProfileURIValue;
            public Uri redditProfileURI
            {
                get { return redditProfileURIValue; }
                set { redditProfileURIValue = value; }
            }

            private string redditUsernameValue;
            public string redditUsername
            {
                get { return redditUsernameValue; }
                set { redditUsernameValue = value; }
            }

            private string fbNamevalue;
            public string fbName
            {
                get { return fbNamevalue; }
                set { fbNamevalue = value; }
            }

            private Uri fbURIvalue;
            public Uri fbURI
            {
                get { return fbURIvalue; }
                set { fbURIvalue = value; }
            }

            private string twitchNamevalue;
            public string twitchName
            {
                get { return twitchNamevalue; }
                set { twitchNamevalue = value; }
            }

            private Uri twitchURIvalue;
            public Uri twitchURI
            {
                get { return twitchURIvalue; }
                set { twitchURIvalue = value; }
            }

            //Serializing only this property and the name
            private bool followedvalue;
            public bool followed
            {
                get { return followedvalue; }
                set { followedvalue = value;}
            }

            // Implement this method to serialize data. The method is called  
            // on serialization. 
            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                // Use the AddValue method to specify serialized values.
                info.AddValue("followed", followedvalue, typeof(bool));
                info.AddValue("liquipediaName", liquipediaName, typeof(string));

            }

            // The special constructor is used to deserialize values. 
            public personObject(SerializationInfo info, StreamingContext context)
            {
                // Reset the property value using the GetValue method.
                followedvalue = (bool) info.GetValue("followed", typeof(bool));
                liquipediaName = (string)info.GetValue("liquipediaName", typeof(string));
            }

            public void displayPersonProperties()
            {
                Console.WriteLine(this.liquipediaName);
                Console.WriteLine(this.liquipediaURI);
                Console.WriteLine(this.irlName);
                Console.WriteLine(this.teamName);
                Console.WriteLine(this.country);
                Console.WriteLine(this.mainRace);
                Console.WriteLine(this.twitchName);
                Console.WriteLine();
            }

            private List<tlPostObject> tlPostListValue;
            public List<tlPostObject> tlPostList
            {
                get { return tlPostListValue; }
                set { tlPostListValue = value;}
            }
        }
        
        public class tlCachedPostPage //A single cached post page; collected by Objects and in one master list
        {
            public tlCachedPostPage()
            {
                //Empty container required to compile
            }

            /// <summary>
            /// Creates an object reference to a Teamliquid.net forum HTML page that has been cached
            /// </summary>
            /// <param name="cachedPageLocation">The local Uri where the cached page HTML file is located</param>
            /// <param name="cachedPageRemoteUri">The original, remote Uri of the cached page</param>
            /// <param name="needsRefresh">True if the page needs to be updated, false if not</param>
            /// <param name="posts">A list object containing references to the tlPostObjects for every post on the cached page</param>
            public tlCachedPostPage(Uri cachedPageLocation,
                                    Uri cachedPageRemoteUri,
                                    bool needsRefresh,
                                    List<tlPostObject> posts)
            {
                //No unique ID at this point... maybe some substring of the URL?
                UniqueID = cachedPageRemoteUri;
            }
            
            private Uri UniqueID;
            public Uri cachedPageLocation
            {
                get { return UniqueID; }
                set { UniqueID = value; }
            }

            private Uri cachedPageRemoteUriValue;
            public Uri cachedPageRemoteUri
            {
                get { return cachedPageRemoteUriValue; }
                set { cachedPageRemoteUriValue = value; }
            }

            private bool needsRefreshValue;
            public bool needsRefresh
            {
                get { return needsRefreshValue; }
                set { needsRefreshValue = value; }
            }

            private List<tlPostObject> postsValue;
            public List<tlPostObject> posts
            {
                get { return postsValue; }
                set { postsValue = value; }
            }
        }
    
        public class tlPostObject
        {
            public tlPostObject()
            {
                //Empty container required to compile
            }

            /// <summary>
            /// Creates an object reference to a specific Comment or Post on TeamLiquid.
            /// </summary>
            /// <param name="uniqueThreadId">The unique integer identifier tied to the TeamLiquid.net/forum thread in which this Post or Comment appears</param>
            /// <param name="threadTitle">The string title of the thread from the TeamLiquid.net forums in which this Post or Comment appears</param>
            /// <param name="threadSection">The Teamliquid.net sub-forum on which the thread for this Post or Comment appears</param>
            /// <param name="commentUri">The Uri address object linking directly to this Post or Comment</param>
            /// <param name="commentNumber">The <![CDATA[ <a name=#> ]]> comment number of this Post or Comment in its parent thread</param>
            /// <param name="postHTMLContent">A string containing the HTML markup content of the Post or Comment</param>
            public tlPostObject(int uniqueThreadId,
                                string threadTitle,
                                string threadSection,
                                Uri commentUri,
                                int commentNumber,
                                string postHTMLContent
                                //DateTime postDateTime
                                //CachePageObject (will be shared with all other postObjects on the same page)
                                )
            {
                UniqueID = uniqueThreadId;   
            }

            private int UniqueID;
            public int uniqueThreadId
            {
                get { return UniqueID; }
                set { UniqueID = value;}
            }

            private string threadTitleValue;
            public string threadTitle
            {
                get { return threadTitleValue; }
                set { threadTitleValue = value; }
            }

            private string threadSectionValue;
            public string threadSection
            {
                get { return threadSectionValue; }
                set { threadSectionValue = value; }
            }

            private Uri commentUriValue;
            public Uri commentUri
            {
                get { return commentUriValue; }
                set { commentUriValue = value; }
            }

            private int commentNumberValue;
            public int commentNumber
            {
                get { return commentNumberValue; }
                set { commentNumberValue = value; }
            }

            private string postContentValue;
            public string postContent
            {
                get { return postContentValue; }
                set { postContentValue = value; }
            }

            //private DateTime postDateTimeValue;
            //public DateTime postDateTime
            //{
            //    get { return postDateTimeValue; }
            //    set { postDateTimeValue = value; }
            //}

        }
    }
}
