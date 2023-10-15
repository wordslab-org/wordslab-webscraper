using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace wordslab.nlptextdoc
{
    public enum NLPTextDocFormat
    {
        TextFile,
        HtmlPreview,
        CsvDataframe
    }

    /// <summary>
    /// Write a NLPTextDocument to a text file on disk
    /// </summary>
    public static class NLPTextDocumentWriter
    {
        public static void WriteToFile(NLPTextDocument doc, string basePath, NLPTextDocFormat docFormat)
        {
            string path = GetFullFilePath(basePath, docFormat);

            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                int lastNestingLevel = 0;
                if (docFormat == NLPTextDocFormat.HtmlPreview)
                {
                    writer.WriteLine("<!doctype html>");
                    writer.WriteLine("<html>");
                    writer.WriteLine("<head>");
                }
                else if (docFormat == NLPTextDocFormat.CsvDataframe)
                {
                    writer.WriteLine("DocEltType;DocEltCmd;NestingLevel;Text;Lang;Chars;Words;AvgWordsLength;LetterChars;NumberChars;OtherChars;HashCode;IsUnique");
                    writer.WriteLine("Document;Start;1;;;;;;;;;;");
                }
                if (docFormat == NLPTextDocFormat.HtmlPreview)
                {
                    writer.WriteLine($"<title>{HttpUtility.HtmlEncode(doc.Title)}</title>");
                }
                else
                {
                    WriteDocumentProperty(writer, NLPTextDocumentFormat.TEXT_DOCUMENT_TITLE, doc.Title, docFormat);
                }
                WriteDocumentProperty(writer, NLPTextDocumentFormat.TEXT_DOCUMENT_URI, doc.Uri, docFormat);
                WriteDocumentProperty(writer, NLPTextDocumentFormat.TEXT_DOCUMENT_TIMESTAMP, doc.Timestamp.ToString(CultureInfo.InvariantCulture), docFormat);
                if (doc.HasMetadata)
                {
                    foreach (var key in doc.Metadata.Keys)
                    {
                        WriteDocumentProperty(writer, NLPTextDocumentFormat.TEXT_DOCUMENT_METADATA, key + "=" + doc.Metadata[key], docFormat);
                    }
                }
                if (docFormat == NLPTextDocFormat.TextFile)
                {
                    writer.WriteLine();
                }
                else if (docFormat == NLPTextDocFormat.HtmlPreview)
                {
                    writer.WriteLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
                    writer.WriteLine("<link href=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css\" rel=\"stylesheet\" integrity=\"sha384-T3c6CoIi6uLrA9TneNEoa7RxnatzjcDSCmG1MXxSR1GAsXEV/Dwwykc2MPK8M2HN\" crossorigin=\"anonymous\">");
                    writer.WriteLine("</head>");
                    writer.WriteLine("<body>");
                }
                if (docFormat == NLPTextDocFormat.CsvDataframe)
                {
                    WriteDocumentElements(writer, doc.Elements, docFormat);
                }
                else
                {
                    WriteDocumentElements(writer, doc.UniqueElements, docFormat);
                }
                if (docFormat == NLPTextDocFormat.HtmlPreview)
                {
                    writer.WriteLine("</body>");
                    writer.WriteLine("</html>");
                }
                else if (docFormat == NLPTextDocFormat.CsvDataframe)
                {
                    writer.WriteLine("Document;End;1;;;;;;;;;;");
                }
            }
        }

        public static string GetFullFilePath(string basePath, NLPTextDocFormat docFormat)
        {
            string path = basePath;
            switch (docFormat)
            {
                case NLPTextDocFormat.TextFile:
                    path += ".nlp.txt";
                    break;
                case NLPTextDocFormat.HtmlPreview:
                    path += ".preview.html";
                    break;
                case NLPTextDocFormat.CsvDataframe:
                    path += ".dataframe.csv";
                    break;
            }

            return path;
        }

        // ## NLPTextDocument Title ...value...
        // ## NLPTextDocument Uri ...value...
        // ## NLPTextDocument Timestamp ...value...
        // ## NLPTextDocument Metadata [key]=...value..
        private static void WriteDocumentProperty(StreamWriter writer, string propertyName, string propertyValue, NLPTextDocFormat docFormat)
        {
            if (docFormat == NLPTextDocFormat.TextFile)
            {
                writer.Write(NLPTextDocumentFormat.TEXT_DOCUMENT_PROPERTY_PREFIX);
                writer.Write(propertyName);
                writer.Write(' ');
                if (!String.IsNullOrEmpty(propertyValue))
                {
                    WriteTextBlock(writer, propertyValue, docFormat);
                }
                else
                {
                    writer.WriteLine();
                }
            }
            else if(docFormat == NLPTextDocFormat.HtmlPreview)
            {
                writer.WriteLine($"<meta name=\"{HttpUtility.HtmlEncode(propertyName)}\" content=\"{HttpUtility.HtmlEncode(propertyValue)}\">");
            }
            else if(docFormat == NLPTextDocFormat.CsvDataframe)
            {
                // DocEltType;DocEltCmd;NestingLevel;Text;Lang;Chars;Words;AvgWordsLength;LetterChars;NumberChars;OtherChars;HashCode;IsUnique
                var textProperties = NLPTextAnalyzer.Instance.AnalyzeText(propertyValue);
                if (propertyName == "Title")
                {
                   writer.WriteLine($"Document;{propertyName};1;\"{textProperties.CSVEncodedText}\";{textProperties.Lang};{textProperties.Chars};{textProperties.Words};{textProperties.AvgWordLength};{textProperties.LetterChars};{textProperties.NumberChars};{textProperties.OtherChars};{textProperties.HashCode};{textProperties.IsUnique}");
                }
                else
                { 
                    writer.WriteLine($"Document;{propertyName};1;\"{textProperties.CSVEncodedText}\";;;;;;;;;");
                }
            }
        }

        private static void WriteDocumentElements(StreamWriter writer, IEnumerable<DocumentElement> elements, NLPTextDocFormat docFormat)
        {
            foreach (var docElement in elements)
            {
                if (docElement.Type == DocumentElementType.TextBlock)
                {
                    var textBlock = (TextBlock)docElement;
                    if (docFormat == NLPTextDocFormat.CsvDataframe)
                    {
                        WriteCsvTextBlock(writer, textBlock);
                    }
                    else
                    {
                        WriteTextBlock(writer, textBlock.Text, docFormat, true, true);
                    }
                }
                else
                {
                    var groupElement = docElement as GroupElement;
                    if (groupElement != null) // always true
                    {
                        bool skipGroupWrapper = false;
                        var groupElementWithTitle = groupElement as GroupElementWithTitle;
                        if (groupElementWithTitle != null && !groupElementWithTitle.HasTitle)
                        {
                            // Skip group wrapper when the group is empty 
                            skipGroupWrapper = groupElement.IsEmpty;
                        }

                        if (!skipGroupWrapper)
                        {
                            // TextFile format only :
                            // Write the list items on one line for readability if they are sufficiently "short"
                            var listElement = groupElement as List;
                            bool writeGroupOnOneLine = listElement != null && listElement.IsShort && docFormat == NLPTextDocFormat.TextFile;
                            if (writeGroupOnOneLine)
                            {
                                WriteListItems(writer, listElement, docFormat);
                            }
                            else
                            {
                                WriteDocumentElementStart(writer, docElement, docFormat);
                                if (docFormat == NLPTextDocFormat.CsvDataframe)
                                {
                                    WriteDocumentElements(writer, groupElement.Elements, docFormat);
                                }
                                else
                                {
                                    WriteDocumentElements(writer, groupElement.UniqueElements, docFormat);
                                }
                                WriteDocumentElementEnd(writer, docElement, docFormat);
                            }
                        }
                    }
                }
            }
        }

        // ## [NestingLevel] [DocumentElementName] Items [Title] >> [item 1] || [item 2] || [item 3]
        private static void WriteListItems(StreamWriter writer, List listElement, NLPTextDocFormat docFormat)
        {
            // This method is only used for NLPTextDocFormat.TextFile

            writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_LINE_MARKER);
            writer.Write(' ');
            writer.Write(listElement.NestingLevel);
            writer.Write(' ');
            writer.Write(NLPTextDocumentFormat.ElemNameForElemType[listElement.Type]);
            writer.Write(' ');
            writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_ITEMS);
            writer.Write(' ');
            if (listElement.HasTitle)
            {
                writer.Write(((GroupElementWithTitle)listElement).Title);
                writer.Write(' ');
            }
            writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_ITEMS_START);
            writer.Write(' ');
            try
            {
                var items = listElement.Elements.Select(item => ((ListItem)item).Elements.OfType<TextBlock>().FirstOrDefault());
                bool isFirstItem = true;
                foreach (var item in items)
                {
                    if (item != null)
                    {
                        if (!isFirstItem)
                        {
                            writer.Write(' ');
                            writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_ITEMS_SEPARATOR);
                            writer.Write(' ');
                        }
                        else
                        {
                            isFirstItem = false;
                        }
                        WriteTextBlock(writer, item.Text, docFormat, false);
                    }
                }
            }
            catch(Exception e)
            {
                // TO DO : remove this when fixing issue #21
            }
            writer.WriteLine();
        }

        // ## [NestingLevel] [Section|List|Table] Start ...title...
        // ## [NestingLevel] ListItem Start
        // ## [NestingLevel] [TableHeader|TableCell] Start row,col
        // ## [NestingLevel] [TableHeader|TableCell] Start row:rowspan,col:colspan
        private static void WriteDocumentElementStart(StreamWriter writer, DocumentElement docElement, NLPTextDocFormat docFormat)
        {
            if (docFormat == NLPTextDocFormat.TextFile)
            {
                if (docElement.Type == DocumentElementType.Section || docElement.Type == DocumentElementType.List ||
                    docElement.Type == DocumentElementType.Table)
                {
                    writer.WriteLine();
                }
                writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_LINE_MARKER);
                writer.Write(' ');
                writer.Write(docElement.NestingLevel);
                writer.Write(' ');
                writer.Write(NLPTextDocumentFormat.ElemNameForElemType[docElement.Type]);
                writer.Write(' ');
                writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_START);
                writer.Write(' ');
                switch (docElement.Type)
                {
                    case DocumentElementType.Section:
                    case DocumentElementType.List:
                    case DocumentElementType.Table:
                        var groupElement = (GroupElementWithTitle)docElement;
                        writer.Write(groupElement.Title);
                        break;
                    case DocumentElementType.TableHeader:
                    case DocumentElementType.TableCell:
                        var tableElement = (TableElement)docElement;
                        if (tableElement.RowSpan == 1 && tableElement.ColSpan == 1)
                        {
                            writer.Write(tableElement.Row);
                            writer.Write(',');
                            writer.Write(tableElement.Col);
                        }
                        else
                        {
                            writer.Write(tableElement.Row);
                            writer.Write(':');
                            writer.Write(tableElement.RowSpan);
                            writer.Write(',');
                            writer.Write(tableElement.Col);
                            writer.Write(':');
                            writer.Write(tableElement.ColSpan);
                        }
                        break;
                }
                writer.WriteLine();
            }
            else if(docFormat == NLPTextDocFormat.HtmlPreview)
            {

                var tableElement = docElement as TableElement;
                if (tableElement != null && tableElement.Col==1)
                {
                    writer.WriteLine("<tr>");
                }

                int headerLevel = Math.Min(docElement.NestingLevel, 6); 
                switch (docElement.Type)
                {
                    case DocumentElementType.Section:
                        writer.Write($"<h{headerLevel}>");
                        break;
                    case DocumentElementType.List:
                    case DocumentElementType.NavigationList:
                        writer.Write("<ul");
                        break;
                    case DocumentElementType.ListItem:
                        writer.Write("<li");
                        break;
                    case DocumentElementType.Table:                        
                        writer.Write("<table");
                        break;
                    case DocumentElementType.TableHeader:
                        writer.Write("<th");
                        break;
                    case DocumentElementType.TableCell:
                        writer.WriteLine("<td");                        
                        break;
                }

                if (tableElement != null)
                {
                    if (tableElement.RowSpan > 1)
                    {
                        writer.Write($" rowspan=\"{tableElement.RowSpan}\"");
                    }
                    if (tableElement.ColSpan > 1)
                    {
                        writer.Write($" colspan=\"{tableElement.ColSpan}\"");
                    }
                }

                var elementWithTitle = docElement as GroupElementWithTitle;
                if(elementWithTitle!=null && elementWithTitle.Title != null)
                {
                    if(docElement.Type == DocumentElementType.Section)
                    {
                        writer.Write(HttpUtility.HtmlEncode(elementWithTitle.Title));
                    }
                    else
                    {
                        writer.Write($" title=\"{HttpUtility.HtmlEncode(elementWithTitle.Title)}\"");
                    }
                }

                if (docElement.Type == DocumentElementType.Section)
                {
                    writer.WriteLine($"</h{headerLevel}>");
                }
                else
                {
                    writer.WriteLine(">");
                }
            }
            else if(docFormat == NLPTextDocFormat.CsvDataframe)
            {
                string elementProperties = null;
                switch (docElement.Type)
                {
                    case DocumentElementType.Section:
                    case DocumentElementType.List:
                    case DocumentElementType.Table:
                        var groupElement = (GroupElementWithTitle)docElement;
                        if (groupElement.TitleProperties != null)
                        {
                            elementProperties = groupElement.TitleProperties.CSVEncodedText;
                        }
                        break;
                    case DocumentElementType.TableHeader:
                    case DocumentElementType.TableCell:
                        var tableElement = (TableElement)docElement;
                        if (tableElement.RowSpan == 1 && tableElement.ColSpan == 1)
                        {
                            elementProperties = $"{tableElement.Row},{tableElement.Col}";
                        }
                        else
                        {
                            elementProperties = $"{tableElement.Row}:{tableElement.RowSpan},{tableElement.Col}:{tableElement.ColSpan}";
                        }
                        break;
                }

                // DocEltType;DocEltCmd;NestingLevel;Text;Lang;Chars;Words;AvgWordsLength;LetterChars;NumberChars;OtherChars;HashCode;IsUnique
                writer.WriteLine($"{NLPTextDocumentFormat.ElemNameForElemType[docElement.Type]};{NLPTextDocumentFormat.DOCUMENT_ELEMENT_START};{docElement.NestingLevel};\"{elementProperties}\";;;;;;;;;");
            }
        }

        // ## [NestingLevel] [DocumentElementName] End
        private static void WriteDocumentElementEnd(StreamWriter writer, DocumentElement docElement, NLPTextDocFormat docFormat)
        {
            if (docFormat == NLPTextDocFormat.TextFile)
            {
                writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_LINE_MARKER);
                writer.Write(' ');
                writer.Write(docElement.NestingLevel);
                writer.Write(' ');
                writer.Write(NLPTextDocumentFormat.ElemNameForElemType[docElement.Type]);
                writer.Write(' ');
                writer.Write(NLPTextDocumentFormat.DOCUMENT_ELEMENT_END);
                var groupElementWithTitle = docElement as GroupElementWithTitle;
                if (groupElementWithTitle != null)
                {
                    if (!String.IsNullOrEmpty(groupElementWithTitle.Title))
                    {
                        writer.Write(" <<");
                        var title = groupElementWithTitle.Title;
                        if (title.Length > 47)
                        {
                            title = title.Substring(0, 47) + "...";
                        }
                        writer.Write(title);
                        writer.Write(">>");
                    }
                }
                writer.WriteLine();
            }
            else if(docFormat == NLPTextDocFormat.HtmlPreview)
            {
                switch (docElement.Type)
                {
                    case DocumentElementType.List:
                    case DocumentElementType.NavigationList:
                        writer.WriteLine("</ul>");
                        break;
                    case DocumentElementType.ListItem:
                        writer.WriteLine("</li>");
                        break;
                    case DocumentElementType.Table:
                        writer.WriteLine("</table>");
                        break;
                    case DocumentElementType.TableHeader:
                        writer.WriteLine("</th>");
                        break;
                    case DocumentElementType.TableCell:
                        writer.WriteLine("</td>");
                        break;
                }
            }
            else if (docFormat == NLPTextDocFormat.CsvDataframe)
            {
                // DocEltType;DocEltCmd;NestingLevel;Text;Lang;Chars;Words;AvgWordsLength;LetterChars;NumberChars;OtherChars;HashCode;IsUnique
                writer.WriteLine($"{NLPTextDocumentFormat.ElemNameForElemType[docElement.Type]};{NLPTextDocumentFormat.DOCUMENT_ELEMENT_END};{docElement.NestingLevel};;;;;;;;;;");
            }
        }

        private static void WriteTextBlock(StreamWriter writer, string text, NLPTextDocFormat docFormat, bool finishLine = true, bool escapeLine = false)
        {
            if (docFormat == NLPTextDocFormat.TextFile)
            {
                if (text.Contains("\n"))
                {
                    text = text.Replace("\n", "\\n");
                }
                if (escapeLine && text.StartsWith("##"))
                {
                    text = ". " + text;
                }
                if (finishLine)
                {
                    writer.WriteLine(text);
                }
                else
                {
                    writer.Write(text);
                }
            }
            else if (docFormat == NLPTextDocFormat.HtmlPreview)
            {
                writer.WriteLine($"<div class=\"p-2\">{HttpUtility.HtmlEncode(text)}</div>");
            }
        }

        private static void WriteCsvTextBlock(StreamWriter writer, TextBlock textBlock)
        {
            // DocEltType;DocEltCmd;NestingLevel;Text;Lang;Chars;Words;AvgWordsLength;LetterChars;NumberChars;OtherChars;HashCode;IsUnique
            var textProperties = textBlock.TextProperties;
            if (textProperties != null)
            {
                writer.WriteLine($"TextBlock;Text;{textBlock.NestingLevel};\"{textProperties.CSVEncodedText}\";{textProperties.Lang};{textProperties.Chars};{textProperties.Words};{textProperties.AvgWordLength};{textProperties.LetterChars};{textProperties.NumberChars};{textProperties.OtherChars};{textProperties.HashCode};{textProperties.IsUnique}");
            }
        }
    }
}