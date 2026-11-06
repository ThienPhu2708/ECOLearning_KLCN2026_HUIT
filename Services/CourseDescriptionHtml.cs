using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace ECOLearning.Services
{
    /// <summary>
    /// Lưu phần HTML do CKEditor tạo ra theo một allow-list nhỏ, phù hợp cho mô tả khóa học.
    /// Không giữ style, script, event handler hoặc media nhúng từ nội dung người dùng.
    /// </summary>
    public static class CourseDescriptionHtml
    {
        private static readonly HashSet<string> AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "p", "h2", "h3", "h4", "ul", "ol", "li", "strong", "b", "em", "i", "u", "s", "del", "a", "blockquote", "br", "hr",
            "table", "thead", "tbody", "tr", "th", "td"
        };

        public static string Sanitize(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            try
            {
                string normalized = Regex.Replace(html.Trim(), @"<br\s*/?>", "<br />", RegexOptions.IgnoreCase);
                normalized = Regex.Replace(normalized, @"<hr\s*/?>", "<hr />", RegexOptions.IgnoreCase);
                normalized = normalized.Replace("&nbsp;", "&#160;");

                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                XDocument document;
                using (var input = new StringReader("<root>" + normalized + "</root>"))
                using (var reader = XmlReader.Create(input, settings)) document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);

                var output = new StringBuilder();
                foreach (XNode node in document.Root.Nodes()) AppendNode(node, output);
                return output.ToString();
            }
            catch
            {
                // Nếu nội dung không phải HTML hợp lệ, lưu thành văn bản thuần để không render markup không tin cậy.
                return HttpUtility.HtmlEncode(html.Trim());
            }
        }

        private static void AppendNode(XNode node, StringBuilder output)
        {
            var text = node as XText;
            if (text != null)
            {
                output.Append(HttpUtility.HtmlEncode(text.Value));
                return;
            }

            var element = node as XElement;
            if (element == null) return;

            string tag = element.Name.LocalName.ToLowerInvariant();
            if (!AllowedTags.Contains(tag))
            {
                foreach (XNode child in element.Nodes()) AppendNode(child, output);
                return;
            }

            if (tag == "br" || tag == "hr")
            {
                output.Append('<').Append(tag).Append(" />");
                return;
            }

            if (tag == "a")
            {
                string href = SafeHref((string)element.Attribute("href"));
                if (href == null)
                {
                    foreach (XNode child in element.Nodes()) AppendNode(child, output);
                    return;
                }
                output.Append("<a href=\"").Append(HttpUtility.HtmlAttributeEncode(href)).Append("\" target=\"_blank\" rel=\"noopener noreferrer\">");
            }
            else output.Append('<').Append(tag).Append('>');

            foreach (XNode child in element.Nodes()) AppendNode(child, output);
            output.Append("</").Append(tag).Append('>');
        }

        private static string SafeHref(string href)
        {
            if (string.IsNullOrWhiteSpace(href)) return null;
            Uri uri;
            if (!Uri.TryCreate(href.Trim(), UriKind.Absolute, out uri)) return null;
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto
                ? uri.AbsoluteUri
                : null;
        }
    }
}
