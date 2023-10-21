using System;
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
        /// Content of the document: first level elements
        /// </summary>
        public IList<DocumentElement> Elements { get; private set; }

        /// <summary>
        /// Percentage of the words in the document which belong to unique text block.
        /// Only available after a full text analysis of the document.
        /// </summary>
        public float PercentUniqueText { get; set; }

        /// <summary>
        /// Content of the document: first level elements filtered ignoring non unique group elements.
        /// </summary>
        public IEnumerable<DocumentElement> UniqueElements
        {
            get
            {
                foreach (var element in Elements)
                {
                    if (element.Type == DocumentElementType.TextBlock)
                    {
                        var textBlock = (TextBlock)element;
                        if (textBlock.TextProperties.IsUnique.Value)
                        {
                            yield return element;
                        }
                    }
                    else if (element is GroupElement)
                    {
                        var groupElement = (GroupElement)element;
                        if (groupElement.ContainsUniqueText)
                        {
                            yield return groupElement;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Iterate over all TextBlocks and Titles of the document with depth-first traversal
        /// while skipping all groups which don't contain any unique text block
        /// </summary>
        public IEnumerable<NLPTextProperties> TextProperties
        {
            get
            {
                foreach(var textProps in GetTextProperties(Elements))
                {
                    yield return textProps;
                }
            }
        }

        private IEnumerable<NLPTextProperties> GetTextProperties(IEnumerable<DocumentElement> elements)
        {
            foreach (var element in elements)
            {
                if (element.Type == DocumentElementType.TextBlock)
                {
                    yield return ((TextBlock)element).TextProperties;
                }
                else if (element is GroupElement)
                {                    
                    if (element is GroupElementWithTitle)
                    {
                        var titleProps = ((GroupElementWithTitle)element).TitleProperties;
                        if (titleProps != null)
                        {
                            yield return titleProps;
                        }
                    }
                    foreach (var textProps in GetTextProperties(((GroupElement)element).Elements))
                    {
                        yield return textProps;
                    }
                }
            }
        }
    }
}
