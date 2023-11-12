using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Parquet.Serialization;
using System.Text;
using wordslab.nlptextdoc;
using System.Threading.Tasks;

namespace wordslab.webscraper.parquet
{
    public static class HuggingFaceDatasetBuilder
    {
        public static async Task GenerateParquetDataset(string baseDirectory, string domain, string lang, string yearMonth, int percentValid=10, int percentTest=10)
        {
            var baseDirectoryInfo = new DirectoryInfo(Path.Combine(baseDirectory, $"{domain}-{lang}-{yearMonth}"));
            if(!baseDirectoryInfo.Exists ) 
            {
                baseDirectoryInfo.Create();
            }            

            Console.WriteLine($"Generating dataset \"{domain} {lang} websites - {yearMonth}\" in directory:");
            Console.WriteLine(baseDirectoryInfo.FullName);
            Console.WriteLine();

            var datasetProperties = new DatasetProperties() { BaseDirectory = baseDirectoryInfo.FullName, Domain = domain, Language = lang, YearMonth = yearMonth };
            foreach(var webisteDir in new DirectoryInfo(baseDirectory).GetDirectories())
            {
                var textFiles = webisteDir.GetFiles("*.text.md", SearchOption.AllDirectories);
                textFiles.Shuffle();

                var languagesCount = textFiles.Select(fileInfo => fileInfo.Name.Substring(fileInfo.Name.Length-10,2)).
                    GroupBy(lang => lang).ToDictionary(group => group.Key, group => group.Count());
                if (languagesCount.ContainsKey(lang) && languagesCount[lang] >= 50)
                {
                    var websiteProperties = new WebsiteProperties() { Name = webisteDir.Name };
                    datasetProperties.Websites.Add(websiteProperties);

                    var textFilesForLang = textFiles.Where(fileInfo => fileInfo.Name.Substring(fileInfo.Name.Length - 10, 2) == lang).ToList();
                    int evalIndex = (int)(textFilesForLang.Count * (100 - percentValid - percentTest) / 100.0);
                    int testIndex = (int)(textFilesForLang.Count * (100 - percentTest) / 100.0);
                    var trainFileLength = await GenerateParquetFile(datasetProperties, websiteProperties, "train", textFilesForLang.Take(evalIndex));
                    var validFileLength = await GenerateParquetFile(datasetProperties, websiteProperties, "valid", textFilesForLang.Skip(evalIndex).Take(testIndex - evalIndex));
                    var testFileLength = await GenerateParquetFile(datasetProperties, websiteProperties, "test", textFilesForLang.Skip(testIndex).Take(textFilesForLang.Count - testIndex));

                    datasetProperties.TrainExamples += evalIndex;
                    datasetProperties.ValidExamples += testIndex - evalIndex;
                    datasetProperties.TestExamples += textFilesForLang.Count - testIndex;
                    datasetProperties.DownloadSize += trainFileLength + validFileLength + testFileLength;
                }
            }

            GenerateDatasetCard(datasetProperties);
        }

        class DatasetProperties
        {
            public string BaseDirectory;

            public string Domain;
            public string Language;
            public string YearMonth;

            public int TrainExamples;
            public int ValidExamples;
            public int TestExamples;

            public long DownloadSize;

            public List<WebsiteProperties> Websites = new List<WebsiteProperties>();
        }

        public class WebsiteProperties
        {
            public string Name;
            public int HtmlPages;
            public int PDFDocuments;
            public int Words;
        }

        private static void GenerateDatasetCard(DatasetProperties datasetProperties)
        {
            var totalExamples = datasetProperties.TrainExamples + datasetProperties.ValidExamples + datasetProperties.TestExamples;
            string sizeCategory = "n<1K";
            if(totalExamples > 1_000_000_000)
            {
                sizeCategory = "1B<n<10B";
            }
            else if (totalExamples > 100_000_000)
            {
                sizeCategory = "100M<n<1B";
            }
            else if (totalExamples > 10_000_000)
            {
                sizeCategory = "10M<n<100M";
            }
            else if (totalExamples > 1_000_000)
            {
                sizeCategory = "1M<n<10M";
            }
            else if (totalExamples > 100_000)
            {
                sizeCategory = "100K<n<1M";
            }
            else if (totalExamples > 10_000)
            {
                sizeCategory = "10K<n<100K";
            }
            else if (totalExamples > 1_000)
            {
                sizeCategory = "1K<n<10K";
            }

            StringBuilder websitesTableRows = new StringBuilder();
            foreach(var website in datasetProperties.Websites)
            {
                websitesTableRows.AppendLine($"| {website.Name} | {website.HtmlPages} | {website.PDFDocuments} | {website.Words} |");
            }

            string datasetCard = $$"""
                ---
                pretty_name: "{{datasetProperties.Domain}} {{datasetProperties.Language}} websites - {{datasetProperties.YearMonth}}"
                tags:
                - wordslab-webscraper

                task_categories:
                - text-generation
                task_ids:
                - language-modeling
                size_categories: {{sizeCategory}}

                language: {{datasetProperties.Language}}
                multilinguality: monolingual

                license: apache-2.0
                source_datasets: original
                language_creators: found
                annotations_creators: no-annotation

                configs:
                - config_name: default
                  data_files:
                  - split: train
                    path: "{{datasetProperties.Domain}}_{{datasetProperties.Language}}_{{datasetProperties.YearMonth}}_train_*.parquet"
                  - split: valid
                    path: "{{datasetProperties.Domain}}_{{datasetProperties.Language}}_{{datasetProperties.YearMonth}}_valid_*.parquet"
                  - split: test
                    path: "{{datasetProperties.Domain}}_{{datasetProperties.Language}}_{{datasetProperties.YearMonth}}_test_*.parquet"

                dataset_info:
                  features:
                    - name: Uri
                      dtype: string
                    - name: ExtractedFromPDF
                      dtype: bool
                    - name: Timestamp
                      dtype: string
                    - name: Lang
                      dtype: string
                    - name: Title
                      dtype: string
                    - name: Text
                      dtype: string
                    - name: Words
                      dtype: int32
                    - name: AvgWordsLength
                      dtype: int32
                    - name: Chars
                      dtype: int32
                    - name: LetterChars
                      dtype: int32
                    - name: NumberChars
                      dtype: int32
                    - name: OtherChars
                      dtype: int32
                  config_name: default
                  splits:
                    - name: train
                      num_examples: {{datasetProperties.TrainExamples}}
                    - name: valid
                      num_examples: {{datasetProperties.ValidExamples}}
                    - name: test
                      num_examples: {{datasetProperties.TestExamples}}
                  download_size: {{datasetProperties.DownloadSize}}
                ---

                # Dataset Card for "{{datasetProperties.Domain}} {{datasetProperties.Language}} websites - {{datasetProperties.YearMonth}}"

                Dataset extracted from public websites by [wordslab-webscraper](https://github.com/wordslab-org/wordslab-webscraper) in {{datasetProperties.YearMonth}}:
                - domain: {{datasetProperties.Domain}}
                - language: {{datasetProperties.Language}}
                - license: Apache 2.0

                ## Dataset Sources

                wordslab-webscraper follows the industry best practices for **polite web scraping**:
                - clearly identifies itself as a known text indexing bot: "bingbot"
                - doesn't try to hide the user IP address behind proxies
                - doesn't try to circumvent bots protection solutions
                - waits for a minimum delay between two pages to avoid generating too much load
                - respects the website "robots.txt" indexing directives 
                - respects the web page Meta Robots HTML tag
                - respects the web page X-Robots-Tag HTTP header
                - respects the web page links rel=nofollow HTML attributes 

                The text data was extracted from the following websites:

                | Website | HTML pages | PDF docs | Words |
                |:---|:---:|:---:|:---:|
                {{websitesTableRows}}

                ## Uses

                **WARNING**
                - **the text included in this dataset belongs to its original authors** and is protected by copyright laws
                - you are not allowed to use this dataset for anything else than **training a large language model**
                - when using a large language model trained on this dataset, you will need to ensure that you comply with the law
                - if you benefit from this large language model, you should try to share the value with the original text authors

                wordslab-webscraper uses an advanced Html to text conversion algorithm optimized for **long context language modeling**:
                - tries to recover the logical structure of the document from the Html or PDF layout
                - preserves document / section / list / table grouping and nesting information
                - **deduplicates text at the website level while preserving the document structure**

                Each example in this dataset is a **markdown text conversion of a full HTML page or PDF document**:
                - the document structure is preserved by markdown syntax: headers, lists, tables, paragraphs
                - all duplicate paragraphs are removed 

                ## Dataset Structure

                The dataset is divided in 3 splits:
                - train: 80% of the data
                - valid: 10% of the data
                - test: 10% of the data

                wordslab-webscraper generates **one parquet file per website and per split**.

                The parquet files are named with the following pattern:
                - {{datasetProperties.Domain}}_{{datasetProperties.Language}}_{{datasetProperties.YearMonth}}_[split]_[website].parquet

                Note than you can load individual splits or websites with HuggingFace datasets using the following commands:

                ```python
                from datasets import load_dataset

                # Load a single plit
                dataset = load_dataset("namespace/{{datasetProperties.Domain}}-{{datasetProperties.Language}}-{{datasetProperties.YearMonth}}", split="train")

                # Load a single website
                data_files = { "train": "{{datasetProperties.Domain}}_{{datasetProperties.Language}}_{{datasetProperties.YearMonth}}_train_[website].parquet", "valid": "{{datasetProperties.Domain}}_{{datasetProperties.Language}}_{{datasetProperties.YearMonth}}_valid_[website].parquet", "test": "{{datasetProperties.Domain}}_{{datasetProperties.Language}}_{{datasetProperties.YearMonth}}_test_[website].parquet" }
                dataset = load_dataset("namespace/{{datasetProperties.Domain}}-{{datasetProperties.Language}}-{{datasetProperties.YearMonth}}", data_files=data_files)
                ```

                Each example in the dataset contains the text of a full web page or PDF document, with the following features:
                - Uri: string
                - ExtractedFromPDF: bool
                - Timestamp: string
                - Lang: string
                - Title: string
                - Text: string
                - Words: int32
                - AvgWordsLength: int32
                - Chars: int32
                - LetterChars: int32
                - NumberChars: int32
                - OtherChars: int32

                Note that beause each example is a full page or document, the "Text" feature can be a pretty long string containing thousands of words (as measured by the "Words" feature): you will typically need to chunk it down to the context size of your large language model before using it.

                ## Bias, Risks, and Limitations

                This dataset is a direct extraction from the source websites.

                It was not manually curated to remove misleading, offensive, or harmful content.

                **Please add a filtering step before using it to train a large language model** if the source websites can't be trusted.

                ## Dataset Card Contact

                Please add a comment in the community section of this repository if you want the maintainer to add or remove websites from this dataset.
                """;

            File.WriteAllText(Path.Combine(datasetProperties.BaseDirectory, "README.md"), datasetCard);
        }

        private static async Task<long> GenerateParquetFile(DatasetProperties datasetProperties, WebsiteProperties websiteProperties, string split, IEnumerable<FileInfo> textFiles)
        {
            var parquetFile = $"{datasetProperties.Domain}_{datasetProperties.Language}_{datasetProperties.YearMonth}_{split}_{websiteProperties.Name}.parquet";
            Console.Write($"- {parquetFile} ");
            var parquetRecords = GenerateParquetRecords(textFiles, datasetProperties, websiteProperties).ToList();
            Console.WriteLine(" OK");

            var parquetFileInfo = new FileInfo(Path.Combine(datasetProperties.BaseDirectory, parquetFile));
            await ParquetSerializer.SerializeAsync(parquetRecords, parquetFileInfo.FullName);
            return parquetFileInfo.Length;
        }

        private static IEnumerable<ParquetRecord> GenerateParquetRecords(IEnumerable<FileInfo> textFiles, DatasetProperties datasetProperties, WebsiteProperties websiteProperties)
        {
            var (x,y) = Console.GetCursorPosition();
            var filesCount = textFiles.Count();
            int counter = 0;
            foreach(var textFile in textFiles)
            {
                counter++;
                if(counter % 100 == 0 || counter == filesCount)
                {
                    Console.SetCursorPosition(x, y);
                    Console.Write($"{counter}/{filesCount}");
                }

                string text = File.ReadAllText(textFile.FullName, Encoding.UTF8);
                var parquetRecord = new ParquetRecord() { Text = text, Lang = datasetProperties.Language };

                var textStats = NLPTextAnalyzer.CountWordsAndChars(text);
                parquetRecord.Words = textStats.Words;
                parquetRecord.AvgWordsLength = textStats.AvgWordLength;
                parquetRecord.Chars = textStats.Chars;
                parquetRecord.LetterChars = textStats.LetterChars;
                parquetRecord.NumberChars = textStats.NumberChars;
                parquetRecord.OtherChars = textStats.OtherChars;

                using (var reader = new StreamReader(textFile.FullName.Replace(".text.md", ".dataframe.csv"), Encoding.UTF8, false, 1024))
                {
                    string line = null;
                    while((line = reader.ReadLine()) != null)
                    {
                        if(line.StartsWith("Document;Title;"))
                        {
                            parquetRecord.Title = GetCsvTextField(line);
                        }
                        else if(line.StartsWith("Document;Uri;"))
                        {
                            parquetRecord.Uri = GetCsvTextField(line);
                            if(parquetRecord.Uri.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase)) 
                            {
                                parquetRecord.ExtractedFromPDF = true;
                            }
                            else
                            {
                                parquetRecord.ExtractedFromPDF = false;
                            }
                        }
                        else if(line.StartsWith("Document;Timestamp;"))
                        {
                            parquetRecord.Timestamp = GetCsvTextField(line); // DateTime.Parse(parquetRecord.Timestamp, CultureInfo.InvariantCulture)
                            break;
                        }
                    }                    
                }

                if (parquetRecord.ExtractedFromPDF)
                {
                    websiteProperties.PDFDocuments++;
                }
                else
                {
                    websiteProperties.HtmlPages++;
                }
                websiteProperties.Words += parquetRecord.Words;

                yield return parquetRecord;
            }
        }

        private static string GetCsvTextField(string line)
        {
            int indexStart = line.IndexOf(";\"");
            int indexEnd = line.LastIndexOf("\";");
            if(indexStart < 0 || indexEnd < 0)
            {
                return String.Empty;
            }
            else
            {
                return line.Substring(indexStart + 2, indexEnd - indexStart - 2);
            }
        }

        public class CsvRecord
        {
            public string DocEltType { get; set; }
            public string DocEltCmd { get; set; }

            public string Text { get; set; }
        }

        public class ParquetRecord
        {
            public string Uri { get; set; }

            public bool ExtractedFromPDF { get; set; }

            public string Timestamp { get; set; }

            public string Lang { get; set; }

            public string Title { get; set; }

            public string Text { get; set; }

            public int Words {  get; set; }
                
            public int AvgWordsLength { get; set; }

            public int Chars { get; set; }
                
            public int LetterChars { get; set; }
                
            public int NumberChars { get; set; }

            public int OtherChars { get; set; }
        }

        private static Random rng = new Random();

        private static void Shuffle<T>(this T[] array)
        {
            int n = array.Length;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = array[k];
                array[k] = array[n];
                array[n] = value;
            }
        }
    }
}
