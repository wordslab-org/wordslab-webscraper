using FastText.NetWrapper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Reflection;
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

        public long HashCode;
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

        public static void AnalyzeDocument(NLPTextDocument normalizedTextDocument, ConcurrentDictionary<long, int> uniqueTextBlocks)
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
            normalizedTextDocument.TotalWords = totalwords;
            normalizedTextDocument.UniqueWords = uniquewords;
            if (docLanguagesDict.Count > 0)
            {
                normalizedTextDocument.Language = docLanguagesDict.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
            }
            foreach (var lang in docLanguagesDict.Keys)
            {
                normalizedTextDocument.Metadata.Add("words-count:"+lang, docLanguagesDict[lang].ToString());
            }
        }

        private static bool AnalyzeDocumentElement(DocumentElement docElement, ConcurrentDictionary<long, int> uniqueTextBlocks)
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
            if (text == null) return null;

            StringBuilder encodedSB = null;
            if (text.IndexOf('"') >= 0)
            {
                encodedSB = new StringBuilder();
            }

            var textStats = CountWordsAndChars(text, encodedSB);

            if (encodedSB != null)
            {
                textStats.CSVEncodedText = encodedSB.ToString();
            }
            else
            {
                textStats.CSVEncodedText = text;
            }

            lock (fastText)
            {
                var preds = fastText.PredictMultiple(text, 2);
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

            textStats.HashCode = ComputeStableHash(text);

            return textStats;
        }

        public static NLPTextProperties CountWordsAndChars(string text, StringBuilder encodedSB = null)
        {
            var textStats = new NLPTextProperties();

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

                    if (Char.IsLetter(c)) { textStats.LetterChars++; }
                    else if (Char.IsNumber(c)) { textStats.NumberChars++; }
                    else { textStats.OtherChars++; }

                    if (c == '"' && encodedSB != null) { encodedSB.Append('"'); }
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

        public static long ComputeStableHash(string text)
        {
            byte[] textBytes = Encoding.UTF8.GetBytes(text);
            byte[] hashBytes =  XxHash64.Hash(textBytes);
            return BitConverter.ToInt64(hashBytes, 0);
        }
    }
}
