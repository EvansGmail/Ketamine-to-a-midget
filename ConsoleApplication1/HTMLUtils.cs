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

namespace HTMLUtils
{
    public class HTMLUtilities
    {
        public static async Task<string> getHTMLStringFromUriAsync(HttpClient client, Uri tlProfileUri)
        {
            using (client)
            {
                var tlProfile_response = await client.GetAsync(tlProfileUri);

                if (tlProfile_response.IsSuccessStatusCode)
                {
                    return await tlProfile_response.Content.ReadAsStringAsync();
                }
                else
                {
                    Console.WriteLine("Uri fetch failed.");
                    return null;
                }
            }
        }

        public static string NameFromURI(string NameIdentifier, string URIStub, Uri fullURI)
        {
            if (fullURI != null)
            {
                string UriString = fullURI.ToString();
                int URI_index = UriString.IndexOf(URIStub);
                if (URI_index == -1)
                {
                    Console.WriteLine("Name not found. Are you sure this is a " + NameIdentifier + "URI?");
                    return null;
                }
                else
                {
                    int name_index = URI_index + URIStub.Length;
                    int name_length = UriString.Length - name_index;
                    return removeCharCodes(UriString.Substring(name_index, name_length));
                }
            }
            else
            {
                Console.WriteLine("Uri was null. Should you even be calling this?");
                return null;
            }
        }

        public static string StringFromTag(string sourceString, string tagStart, string tagClose)
        {
            return StringFromTag(sourceString, tagStart, tagClose, 0);
        }

        public static string StringFromTag(string sourceString, string tagStart, string tagClose, int startPos)
        {
            int tagStart_index = sourceString.IndexOf(tagStart, startPos);
            if (tagStart_index == -1)
            {
                return null;
            }
            else
            {
                int tagEnd_index = sourceString.IndexOf(tagClose, tagStart_index + tagStart.Length) + tagClose.Length;
                if (tagEnd_index == -1)
                {
                    Console.WriteLine("Found opening tag, but not closing tag! (Did you close a different tag or is the HTML malformed?");
                    return null;
                }
                else
                {
                    int tag_length = tagEnd_index - tagStart_index;
                    return sourceString.Substring(tagStart_index, tag_length);
                }
            }
        }

        public static Uri hrefUriFromTitle(string sourceString, string title_name)
        {
            int title_index = sourceString.IndexOf("title=\"" + title_name + "\"");
            if (title_index != -1)
            {
                int tag_index = sourceString.LastIndexOf("<a href=\"", title_index);
                int href_start = sourceString.IndexOf("\"", tag_index) + 1;
                int href_end = sourceString.IndexOf("\"", href_start);
                int href_length = href_end - href_start;
                return new Uri(sourceString.Substring(href_start, href_length));
            }
            else
            {
                Console.WriteLine("No " + title_name + " found!");
                return null;
            }
        }

        public static string InnerText(string inputHTML)
        {
            return InnerText(inputHTML, 0, inputHTML.Length);
        }

        public static string InnerText(string inputHTML, int start, int end)
        {
            int nesting = 0;
            string nestString = "<";
            string unnestString = ">";
            string innerTextString = "";
            string oneCharacter;

            //This is potentially confusing, because I call it "nesting" when it's really just keeping track of brackets,
            // and not nested brackets (e.g. table brackets). I think I could just start capturing after a ">"
            // until I reach a "<" without actually keeping track of the nesting... but leaving it as is for now.

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
            }
            else
                return innerTextString;
        }

        public static string removeCharCodes(string inputString)
        {
            return inputString.Replace("&#160;", "")
                              .Replace("%20", " ")
                              .Replace("%5B", "[") //You can thank Day[9] for making me add these two
                              .Replace("%5D", "]"); // ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
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

        public static string grabHREF(string sourceString)
        {
            int hrefLocation = sourceString.IndexOf("href");
            string quoteType = "\"";
            int uriStart = new int();

            if (hrefLocation != -1)
            {
                uriStart = sourceString.IndexOf("\"", hrefLocation) + 1;
            }
            else uriStart = 0;

            if ((hrefLocation != -1) && ((uriStart > hrefLocation + 10) || (uriStart == 0)))
            {
                uriStart = sourceString.IndexOf("'", hrefLocation) + 1;
                quoteType = "'";
            }

            int uriEnd = new int();

            if ((uriStart < hrefLocation + 10) && (uriStart != 0))
            {
                uriEnd = sourceString.IndexOf(quoteType, uriStart);
            }
            else uriEnd = -1;

            int uriLength = uriEnd - uriStart;

            if ((hrefLocation != -1) && (uriStart != 0) && (uriEnd != -1) && (uriStart < uriEnd))
            {
                return sourceString.Substring(uriStart, uriLength);
            }
            else
            {
                //Console.WriteLine("No link tags found!"); //This was really annoying before I commented it out.
                return null;
            }
        }

        public static string StringFromParameter(string sourceHTML, string param)
        {
            char[] matchQuotes = {'\'', '\"'};
            int param_index = sourceHTML.IndexOf(param, 0);
            if (param_index != -1)
            {
                int valueStart = sourceHTML.IndexOfAny(matchQuotes, param_index); //valueStart = index of "
                if (valueStart != -1)
                {
                    int valueEnd = sourceHTML.IndexOf(sourceHTML[valueStart], valueStart + 1); //valueEnd = index of "
                    int valueLength = valueEnd - valueStart - 1; //Length of whole thing, without quotes...right?
                    if ((valueEnd != -1) && (valueLength > 0))
                    {
                        return sourceHTML.Substring(valueStart + 1, valueLength);
                    }
                    else
                    {
                        Console.WriteLine("HTML malformed; no parameter found.");
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine("No parameter value found. Are there no quotes?");
                    return null;
                }
            }
            else
            {
                Console.WriteLine("Parameter \"" + param + "\" not found.");
                return null;
            }
        }
    }
}