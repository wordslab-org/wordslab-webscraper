﻿using System;
using System.Collections.Generic;

namespace wordslab.nlptextdoc
{
    /// <summary>
    /// Use NLPTextDocumentBuilder or NLPTextDocumentReader.ReadFromFile
    /// to build a NLPTextDocument object
    /// </summary>
    public class NLPTextDocument
    {        
        internal NLPTextDocument(string uri)
        {
            Uri = uri;
            Timestamp = DateTime.Now;
            Elements = new List<DocumentElement>();
        }

        /// <summary>
        /// General title of the document
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Source Uri where the document was extracted from
        /// </summary>
        public string Uri { get; internal set; }

        /// <summary>
        /// Date and time at which the document was extracted
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Any king of metadata can be attached to this document
        /// </summary>
        public IDictionary<string, string> Metadata
        {
            get
            {
                if (metadataDictionary == null) metadataDictionary = new Dictionary<string, string>();
                return metadataDictionary;
            }
        }
        private IDictionary<string, string> metadataDictionary;

        /// <summary>
        /// True if the document has attached metadata
        /// </summary>
        public bool HasMetadata { get { return metadataDictionary != null; } }

        /// <summary>
        /// Content of the document
        /// </summary>
        public IList<DocumentElement> Elements { get; private set; }

        /// <summary>
        /// Iterate over all TextBlocks and Titles of the documents with depth-first traversal
        /// </summary>
        public IEnumerable<string> TextStrings
        {
            get
            {
                foreach(var str in GetTextStrings(Elements))
                {
                    yield return str;
                }
            }
        }

        private IEnumerable<string> GetTextStrings(IEnumerable<DocumentElement> elements)
        {
            foreach (var element in elements)
            {
                if (element.Type == DocumentElementType.TextBlock)
                {
                    yield return ((TextBlock)element).Text;
                }
                else if (element is GroupElement)
                {
                    if (element is GroupElementWithTitle)
                    {
                        var title = ((GroupElementWithTitle)element).Title;
                        if (!String.IsNullOrEmpty(title))
                        {
                            yield return title;
                        }
                    }
                    foreach (var str in GetTextStrings(((GroupElement)element).Elements))
                    {
                        yield return str;
                    }
                }
            }
        }
    }
}
