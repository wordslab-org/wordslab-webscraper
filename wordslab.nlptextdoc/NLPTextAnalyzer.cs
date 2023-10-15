using FastText.NetWrapper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace wordslab.nlptextdoc
{
    public class NLPTextProperties
    {
        public int Chars;
        public int LetterChars;
        public int NumberChars;
        public int OtherChars;
        public int WhitespaceChars { get { return Chars - LetterChars - NumberChars - OtherChars; } }

        public int Words;
        public int AvgWordLength;
        
        public string Lang;

        public int HashCode;
        public bool? IsUnique;

        public string CSVEncodedText;
    }

    public class NLPTextAnalyzer
    {
        private static NLPTextAnalyzer instance;

        public static NLPTextAnalyzer Instance
        {
            get
            {
                if(instance == null) { instance = new NLPTextAnalyzer(); }
                return instance;
            }
        }

        private FastTextWrapper fastText;

        private NLPTextAnalyzer()
        {
            fastText = new FastTextWrapper();
            Assembly assembly = Assembly.GetAssembly(typeof(NLPTextAnalyzer));
            byte[] languageIdentificationModelBytes = null;
            using (Stream stream = assembly.GetManifestResourceStream("wordslab.nlptextdoc.lid.176.ftz"))
            {
                // Read the bytes from the stream
                languageIdentificationModelBytes = new byte[stream.Length];
                stream.Read(languageIdentificationModelBytes, 0, languageIdentificationModelBytes.Length);
            }
            fastText.LoadModel(languageIdentificationModelBytes);
        }

        public static void AnalyzeDocument(NLPTextDocument normalizedTextDocument, ConcurrentDictionary<int, int> uniqueTextBlocks)
        {
            // Analyze all document elements in recursive way (depth-first)
            // and attach a NLPTextProperties object to each document element containing text
            foreach (var docElement in normalizedTextDocument.Elements)
            {
                AnalyzeDocumentElement(docElement, uniqueTextBlocks);
            }

            // Compute stats on all text properties
            int uniquewords = 0;
            int totalwords = 0;
            var docLanguagesDict = new Dictionary<string, int>();
            foreach (var textProperties in normalizedTextDocument.TextProperties)
            {
                totalwords += textProperties.Words;
                if (textProperties.IsUnique.Value)
                {
                    uniquewords += textProperties.Words;
                }           
                if (!docLanguagesDict.ContainsKey(textProperties.Lang))
                {
                    docLanguagesDict.Add(textProperties.Lang, textProperties.Words);
                }
                else
                {
                    docLanguagesDict[textProperties.Lang] += textProperties.Words;
                }
            }

            // Store stats in the document
            normalizedTextDocument.PercentUniqueText = uniquewords / (float)totalwords;
            foreach (var lang in docLanguagesDict.Keys)
            {
                normalizedTextDocument.Metadata.Add("words-count:"+lang, docLanguagesDict[lang].ToString());
            }
        }

        private static bool AnalyzeDocumentElement(DocumentElement docElement, ConcurrentDictionary<int, int> uniqueTextBlocks)
        {
            // 1. Analyze elements with text
            NLPTextProperties textProperties = null;
            switch (docElement.Type)
            {
                case DocumentElementType.Section:
                case DocumentElementType.List:
                case DocumentElementType.Table:
                    var docElementWithTitle = (GroupElementWithTitle)docElement;
                    if (!String.IsNullOrEmpty(docElementWithTitle.Title))
                    {
                        docElementWithTitle.TitleProperties = Instance.AnalyzeText(docElementWithTitle.Title);
                        textProperties = docElementWithTitle.TitleProperties;
                    }
                    break;
                case DocumentElementType.TextBlock:
                    var textBlock = (TextBlock)docElement;
                    textBlock.TextProperties = Instance.AnalyzeText(textBlock.Text);
                    textProperties = textBlock.TextProperties;
                    break;
            }
            if (textProperties != null)
            {
                if (uniqueTextBlocks.TryAdd(textProperties.HashCode, textProperties.Words))
                {
                    textProperties.IsUnique = true;
                }
                else
                {
                    textProperties.IsUnique = false;
                }
            }

            // 2. Depth-first traversal of group elements
            if (docElement is GroupElement)
            {
                var groupElement = (GroupElement)docElement;
                foreach(var childElement in groupElement.Elements)
                {
                    groupElement.ContainsUniqueText |= AnalyzeDocumentElement(childElement, uniqueTextBlocks);
                }
                return groupElement.ContainsUniqueText;
            }
            // TextBlock
            else
            {
                return textProperties.IsUnique.Value;
            }
        }

        public NLPTextProperties AnalyzeText(string text)
        {
            var textStats = new NLPTextProperties();

            StringBuilder encodedSB = null;
            if (text.IndexOf('"') >= 0)
            {
                encodedSB = new StringBuilder();
            }

            bool inWord = false;
            int wordStartIndex = -1;
            int charIndex = -1;
            textStats.Chars = text.Length;
            foreach (char c in text)
            {
                charIndex++;
                if (encodedSB != null) { encodedSB.Append(c); }
                if (char.IsWhiteSpace(c))
                {
                    if (inWord)
                    {
                        textStats.Words++;
                        textStats.AvgWordLength += charIndex - wordStartIndex;
                        inWord = false;
                        wordStartIndex = -1;
                    }                    
                }
                else 
                {
                    if (!inWord)
                    {
                        inWord = true;
                        wordStartIndex = charIndex;
                    }

                    if(Char.IsLetter(c)) { textStats.LetterChars++; }
                    else if (Char.IsNumber(c)) { textStats.NumberChars++; }
                    else { textStats.OtherChars++; }

                    if (c == '"') { encodedSB.Append('"'); }
                }
            }
            if (inWord)
            {
                textStats.Words++;
                textStats.AvgWordLength += charIndex - wordStartIndex;
                inWord = false;
                wordStartIndex = -1;
            }
            if (textStats.Words > 0) 
            {
                textStats.AvgWordLength /= textStats.Words;
            }
            if (encodedSB != null)
            {
                textStats.CSVEncodedText = encodedSB.ToString();
            }
            else
            {
                textStats.CSVEncodedText = text;
            }

            lock(fastText)
            {
                var preds = fastText.PredictMultiple(text,2);
                if (preds.Length > 0)
                {
                    if (preds[0].Probability > 0.6)
                    {
                        textStats.Lang = GetLanguageCode(preds[0].Label);
                    }
                    /*else if (preds[0].Probability > 0.3 && preds[0].Probability / preds[1].Probability >= 1.9)
                    {
                        textStats.Lang = GetLanguageCode(preds[0].Label);
                    }*/
                    else
                    {
                        textStats.Lang = "?";
                    }
                }
                else
                {
                    textStats.Lang = "?";
                }
            }

            textStats.HashCode = text.GetHashCode();

            return textStats;
        }

        private static string GetLanguageCode(string predsLabel)
        {
            if (predsLabel.Length <= 2)
            {
                return predsLabel;
            }
            else
            {
                return predsLabel.Substring(predsLabel.Length - 2);
            }
        }

        public static string ComputeMd5Hash(string text)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text));

                // Convert byte array to a string
                StringBuilder hashString = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    hashString.Append(hashBytes[i].ToString("x2"));
                }

                return hashString.ToString();
            }
        }
    }
}
