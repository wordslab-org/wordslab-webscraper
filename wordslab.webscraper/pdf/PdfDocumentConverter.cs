using wordslab.nlptextdoc;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace wordslab.webscraper.pdf
{
    /// <summary>
    /// Converts a PDF file parsed by PdfPig to a simplified NLPTextDocument
    /// </summary>
    public static class PdfDocumentConverter
    {
        /// <summary>
        /// Start of the tree traversal at the document level
        /// </summary>
        public static NLPTextDocument ConvertToNLPTextDocument(string absoluteUri, PdfDocument pdfDocument)
        {
            var docBuilder = new NLPTextDocumentBuilder(absoluteUri);

            // Document metadata extraction
            docBuilder.SetTitle(pdfDocument.Information.Title);
            var metadataDict = pdfDocument.Information.DocumentInformationDictionary.Data;
            foreach (var key in metadataDict.Keys)
            {
                if(key != "Title" && metadataDict[key] is UglyToad.PdfPig.Tokens.StringToken)
                {
                    docBuilder.AddMetadata(key, ((UglyToad.PdfPig.Tokens.StringToken)metadataDict[key]).Data);
                }
            }

            // Document layout parsing
            List<List<UglyToad.PdfPig.DocumentLayoutAnalysis.TextBlock>> pagesOfTextBlocks = new List<List<UglyToad.PdfPig.DocumentLayoutAnalysis.TextBlock>>(pdfDocument.NumberOfPages);
            for (var i = 1; i <= pdfDocument.NumberOfPages; i++)
            {
                var page = pdfDocument.GetPage(i);

                // Detect text blocks on the page using default parameters
                // - within line angle is set to [-30, +30] degree (horizontal angle)
                // - between lines angle is set to [45, 135] degree (vertical angle)
                // - between line multiplier is 1.3
                var words = page.GetWords(NearestNeighbourWordExtractor.Instance);
                var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
                var orderedBlocks = UnsupervisedReadingOrderDetector.Instance.Get(blocks).ToList();

                pagesOfTextBlocks.Add(orderedBlocks);
            }

            // Heuristic to detect page numbers and other artifacts we want to ignore
            var documentTextBlocksQuery = pagesOfTextBlocks.SelectMany(page => page);
            if (pdfDocument.NumberOfPages > 1)
            {
                var textBlocksToIgnore = DecorationTextBlockClassifier.Get(pagesOfTextBlocks).SelectMany(page => page);
                documentTextBlocksQuery = documentTextBlocksQuery.Except(textBlocksToIgnore);
            }

            // Heuristic to detect the title heights
            var documentTextBlocks = documentTextBlocksQuery.ToList();
            if (documentTextBlocks.Count > 0)
            {
                var titlesLineHeights = new Stack<double>();
                for (int idx = 0; idx < (documentTextBlocks.Count - 1); idx++)
                {
                    var currentBlock = documentTextBlocks[idx];     
                    
                    // Rule: ignore text blocks with only one char
                    if(currentBlock.Text.Trim().Length <= 1) { continue; }

                    // Rule 1: a title can not span more than 2 lines
                    if (currentBlock.TextLines.Count > 2)
                    {
                        docBuilder.AddTextBlock(currentBlock.Text);
                    }
                    else
                    {
                        // Rule 2: the font a title is at least 20% taller than the following line
                        var nextBlock = documentTextBlocks[idx + 1];
                        var currentLineHeight = currentBlock.TextLines[0].BoundingBox.Height;
                        var nextLineHeight = nextBlock.TextLines[0].BoundingBox.Height;
                        if(currentLineHeight / nextLineHeight > 1.2 && currentLineHeight > 8)
                        {
                            // Find the title nesting Level                            
                            while (titlesLineHeights.Count > 0 && currentLineHeight / titlesLineHeights.Peek() >= 1.2)
                            {
                                titlesLineHeights.Pop();
                                docBuilder.EndSection();
                            }
                            docBuilder.StartSection(currentBlock.Text);
                        }
                        else
                        {
                            docBuilder.AddTextBlock(currentBlock.Text);
                        }
                    }
                }
                docBuilder.AddTextBlock(documentTextBlocks[documentTextBlocks.Count - 1].Text);
                if(titlesLineHeights.Count > 0)
                {
                    for (int i = 0; i < titlesLineHeights.Count; i++) 
                    {
                        docBuilder.EndSection();
                    }
                }
             }

            return docBuilder.TextDocument;
        }
    }
}
