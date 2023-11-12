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
        CsvDataframe,
        HtmlPreview,
        MarkdownText
    }

    /// <summary>
    /// Write a NLPTextDocument to a text file on disk
    /// </summary>
    public static class NLPTextDocumentWriter
    {
        public static void WriteToFile(NLPTextDocument doc, string basePath, NLPTextDocFormat docFormat)
        {
            string path = GetFullFilePath(doc, basePath, docFormat);

            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                // Markdown document model is much more restricted than HTML for bested content
                // => when generating markdown output, we need to keep track of
                //    NLPTextDocument nesting context to simplify it as appropriate
                Stack<MarkdownNestingState> markdownNestingStates = null;
                if(docFormat == NLPTextDocFormat.MarkdownText)
                {
                    markdownNestingStates = new Stack<MarkdownNestingState>();
                }

                // Generate the document header
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
                if (docFormat != NLPTextDocFormat.MarkdownText)
                {
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
                }
                if (docFormat == NLPTextDocFormat.HtmlPreview)
                {
                    writer.WriteLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
                    writer.WriteLine("<link href=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css\" rel=\"stylesheet\" integrity=\"sha384-T3c6CoIi6uLrA9TneNEoa7RxnatzjcDSCmG1MXxSR1GAsXEV/Dwwykc2MPK8M2HN\" crossorigin=\"anonymous\">");
                    writer.WriteLine("</head>");
                    writer.WriteLine("<body>");
                    writer.WriteLine($"<div class=\"p-2 \"><a href=\"{doc.Uri}\" class=\"btn btn-primary\" target=\"_blank\">{doc.Uri}</a></div>");
                }

                // Generate the document content
                if (docFormat == NLPTextDocFormat.CsvDataframe)
                {
                    WriteDocumentElements(writer, doc.Elements, docFormat, markdownNestingStates);
                }
                else
                {
                    WriteDocumentElements(writer, doc.UniqueElements, docFormat, markdownNestingStates);
                }

                // Generate the document footer
                if (docFormat == NLPTextDocFormat.HtmlPreview)
                {
                    writer.WriteLine("</body>");
                    writer.WriteLine("</html>");
                }
                else if (docFormat == NLPTextDocFormat.CsvDataframe)
                {
                    writer.WriteLine("Document;End;1;;;;;;;;;;");
                }
                else if(docFormat == NLPTextDocFormat.MarkdownText)
                {
                    writer.WriteLine();
                }
            }
        }

        public static string GetFullFilePath(NLPTextDocument doc, string basePath, NLPTextDocFormat docFormat)
        {
            string path = basePath;

            // Add language extension
            var langageCode = "xx";
            if(doc.Language != "?")
            {
                langageCode = doc.Language;
            }
            path += $".{langageCode}";

            // Add format extension
            switch (docFormat)
            {
                case NLPTextDocFormat.HtmlPreview:
                    path += ".preview.html";
                    break;
                case NLPTextDocFormat.CsvDataframe:
                    path += ".dataframe.csv";
                    break;
                case NLPTextDocFormat.MarkdownText:
                    path += ".text.md";
                    break;
            }

            return path;
        }

        private static void WriteDocumentProperty(StreamWriter writer, string propertyName, string propertyValue, NLPTextDocFormat docFormat)
        {
            // Replace null value by empty value before writing it to a file
            if (propertyValue == null) propertyValue="";

            if (docFormat == NLPTextDocFormat.HtmlPreview)
            {
                writer.WriteLine($"<meta name=\"{HttpUtility.HtmlEncode(propertyName)}\" content=\"{HttpUtility.HtmlEncode(propertyValue)}\">");
            }
            else if (docFormat == NLPTextDocFormat.CsvDataframe)
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
            else if(docFormat == NLPTextDocFormat.MarkdownText)
            {
                writer.WriteLine($"- {propertyName}: {propertyValue}");
            }
        }

        private static void WriteDocumentElements(StreamWriter writer, IEnumerable<DocumentElement> elements, NLPTextDocFormat docFormat, Stack<MarkdownNestingState> markdownNestingStates)
        {
            foreach (var docElement in elements)
            {
                if (docElement.Type == DocumentElementType.TextBlock)
                {
                    var textBlock = (TextBlock)docElement;
                    WriteTextBlock(writer, textBlock, docFormat, markdownNestingStates);
                }
                else
                {
                    var groupElement = (GroupElement)docElement;
                    
                    // Skip group if it is empty and has no title
                    bool skipEmptyGroup = false;
                    var groupElementWithTitle = groupElement as GroupElementWithTitle;
                    if (groupElementWithTitle != null && !groupElementWithTitle.HasTitle)
                    {                        
                        skipEmptyGroup = groupElement.IsEmpty;
                    }

                    if (!skipEmptyGroup)
                    {
                        IEnumerable<DocumentElement> childElements = null;
                        // All elements are written to the Csv file
                        if (docFormat == NLPTextDocFormat.CsvDataframe)
                        {
                            childElements = groupElement.Elements;
                        }
                        // Only unique elements are written to the preview formats
                        else
                        {
                            childElements = groupElement.UniqueElements;
                        }

                        // Special case: for nested lists, the second level list shouldn't be the first element
                        var firstElement = childElements.FirstOrDefault();
                        if (groupElement.Type == DocumentElementType.ListItem && firstElement != null && (firstElement.Type == DocumentElementType.List || firstElement.Type == DocumentElementType.NavigationList) && childElements.Count() > 1)
                        {
                            var reorderedChildElements = new List<DocumentElement>();
                            foreach(var childElement in childElements)
                            {
                                // Skip first list element
                                if(childElement == firstElement)
                                {
                                    continue;
                                }
                                // Add second level list
                                if(childElement.Type != DocumentElementType.TextBlock && firstElement != null)
                                {
                                    reorderedChildElements.Add(firstElement);
                                    firstElement = null;
                                }
                                reorderedChildElements.Add(childElement);
                            }
                            if(firstElement != null)
                            {
                                reorderedChildElements.Add(firstElement);
                                childElements = reorderedChildElements;
                            }
                        }

                        // Write group element
                        WriteDocumentElementStart(writer, docElement, docFormat, markdownNestingStates);
                        WriteDocumentElements(writer, childElements, docFormat, markdownNestingStates);
                        WriteDocumentElementEnd(writer, docElement, docFormat, markdownNestingStates);
                    }
                }

                // Distinguish first and following elements inside a list item group
                var nestingState = markdownNestingStates?.Count > 0 ? markdownNestingStates.Peek() : null;
                if(nestingState != null && nestingState.IsInsideTableCell)
                {
                    nestingState.IsFirstElementInsideListItem = false;
                }
            }
        }
                
        private static void WriteDocumentElementStart(StreamWriter writer, DocumentElement docElement, NLPTextDocFormat docFormat, Stack<MarkdownNestingState> markdownNestingStates)
        {
            if(docFormat == NLPTextDocFormat.HtmlPreview)
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
            else if (docFormat == NLPTextDocFormat.MarkdownText)
            {
                var currentNestingState = markdownNestingStates?.Count > 0 ? markdownNestingStates.Peek() : null;
                if (currentNestingState == null || !currentNestingState.IsInsideTableCell)
                {
                    var tableElement = docElement as TableElement;
                    if (tableElement != null && tableElement.Col == 1 && tableElement.Row > 1)
                    {
                        writer.WriteLine("|");
                    }

                    switch (docElement.Type)
                    {
                        case DocumentElementType.Section:
                            var headerLevel = Math.Min(docElement.NestingLevel, 6);
                            for (int i = 0; i < headerLevel; i++) writer.Write('#');
                            writer.Write(' ');
                            break;
                        case DocumentElementType.ListItem:
                            for (int i = 0; i < markdownNestingStates.Count; i++) writer.Write("  ");
                            writer.Write("- ");
                            break;
                        case DocumentElementType.TableHeader:
                        case DocumentElementType.TableCell:
                            writer.Write("| ");
                            break;
                    }

                    var elementWithTitle = docElement as GroupElementWithTitle;
                    if (elementWithTitle != null && elementWithTitle.Title != null)
                    {
                        writer.WriteLine(elementWithTitle.Title);
                        writer.WriteLine();
                    }
                }

                if (docElement.Type == DocumentElementType.ListItem)
                {
                    var nestingState = new MarkdownNestingState() { IsInsideListItem = true };
                    markdownNestingStates.Push(nestingState);
                }
                else if (docElement.Type == DocumentElementType.TableCell || docElement.Type == DocumentElementType.TableHeader)
                {
                    var nestingState = new MarkdownNestingState() { IsInsideTableCell = true };
                    markdownNestingStates.Push(nestingState);
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

        private static void WriteDocumentElementEnd(StreamWriter writer, DocumentElement docElement, NLPTextDocFormat docFormat, Stack<MarkdownNestingState> markdownNestingStates)
        {
            if(docFormat == NLPTextDocFormat.HtmlPreview)
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
            else if (docFormat == NLPTextDocFormat.MarkdownText)
            {
                var currentNestingState = markdownNestingStates?.Count > 0 ? markdownNestingStates.Peek() : null; 
                if (currentNestingState == null || !currentNestingState.IsInsideTableCell)
                {
                    switch (docElement.Type)
                    {
                        case DocumentElementType.List:
                        case DocumentElementType.NavigationList:
                            writer.WriteLine();
                            break;
                        case DocumentElementType.Table:
                            writer.WriteLine("|");
                            writer.WriteLine();
                            break;
                        case DocumentElementType.TableHeader:
                        case DocumentElementType.TableCell:
                            writer.Write(" ");
                            break;
                    }
                }


                if (docElement.Type == DocumentElementType.ListItem ||
                    docElement.Type == DocumentElementType.TableCell || docElement.Type == DocumentElementType.TableHeader)
                {
                    markdownNestingStates.Pop();
                }
            }
            else if (docFormat == NLPTextDocFormat.CsvDataframe)
            {
                // DocEltType;DocEltCmd;NestingLevel;Text;Lang;Chars;Words;AvgWordsLength;LetterChars;NumberChars;OtherChars;HashCode;IsUnique
                writer.WriteLine($"{NLPTextDocumentFormat.ElemNameForElemType[docElement.Type]};{NLPTextDocumentFormat.DOCUMENT_ELEMENT_END};{docElement.NestingLevel};;;;;;;;;;");
            }
        }

        private static void WriteTextBlock(StreamWriter writer, TextBlock textBlock, NLPTextDocFormat docFormat, Stack<MarkdownNestingState> markdownNestingStates)
        {
            if (docFormat == NLPTextDocFormat.CsvDataframe)
            {
                // DocEltType;DocEltCmd;NestingLevel;Text;Lang;Chars;Words;AvgWordsLength;LetterChars;NumberChars;OtherChars;HashCode;IsUnique
                var textProperties = textBlock.TextProperties;
                if (textProperties != null)
                {
                    writer.WriteLine($"TextBlock;Text;{textBlock.NestingLevel};\"{textProperties.CSVEncodedText}\";{textProperties.Lang};{textProperties.Chars};{textProperties.Words};{textProperties.AvgWordLength};{textProperties.LetterChars};{textProperties.NumberChars};{textProperties.OtherChars};{textProperties.HashCode};{textProperties.IsUnique}");
                }
            }
            else if (docFormat == NLPTextDocFormat.MarkdownText)
            {
                var nestingState = markdownNestingStates?.Count > 0 ? markdownNestingStates.Peek() : null;
                if (nestingState == null)
                {
                    writer.WriteLine(textBlock.Text);
                    writer.WriteLine();
                }
                else if(nestingState.IsInsideListItem)
                {
                    if(!nestingState.IsFirstElementInsideListItem)
                    {
                        for (int i = 0; i <= markdownNestingStates.Count; i++) writer.Write("  ");
                    }
                    writer.WriteLine(textBlock.Text);
                }
                else if(nestingState.IsInsideTableCell)
                {
                    writer.Write(textBlock.Text);
                    writer.Write(" ");
                }
            }
            else if (docFormat == NLPTextDocFormat.HtmlPreview)
            {
                writer.WriteLine($"<div class=\"p-2\">{HttpUtility.HtmlEncode(textBlock.Text)}</div>");
            }
        }

        class MarkdownNestingState
        {
            public bool IsInsideListItem = false;
            public bool IsFirstElementInsideListItem = true;

            public bool IsInsideTableCell = false;
        }
    }
}