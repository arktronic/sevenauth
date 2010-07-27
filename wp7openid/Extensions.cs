using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace wp7openid
{
    public static class Extensions
    {
        /// <summary>
        /// Returns a URL query-style string in the form of "key1=value1&key2=value2..." without the initial ampersand or question mark.
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        public static string ToUrlQuery(this Dictionary<string, string> dict)
        {
            var sb = new StringBuilder();
            foreach (var entry in dict)
            {
                sb.Append(HttpUtility.UrlEncode(entry.Key));
                sb.Append("=");
                sb.Append(HttpUtility.UrlEncode(entry.Value));
                sb.Append("&");
            }
            sb.Remove(sb.Length - 1, 1);

            return sb.ToString();
        }
    }
}
