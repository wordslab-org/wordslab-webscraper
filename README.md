# wordslab web scraper

## Installation

### Windows

```
set installdir=%HOMEPATH%\wordslab-webscraper
mkdir %installdir%
curl -L -o %installdir%\wordslab-webscraper-win-x64.zip https://github.com/wordslab-org/wordslab-webscraper/releases/download/v1.0.0/wordslab-webscraper-win-x64.zip
tar -x -f %installdir%\wordslab-webscraper-win-x64.zip -C %installdir%
del %installdir%\wordslab-webscraper-win-x64.zip
cd %installdir%
wordslab-webscraper
```

### Linux

```
installdir=$HOME/wordslab-webscraper
mkdir $installdir
curl -L -o $installdir/wordslab-webscraper-linux-x64.tar.gz https://github.com/wordslab-org/wordslab-webscraper/releases/download/v1.0.0/wordslab-webscraper-linux-x64.tar.gz
tar -xf $installdir/wordslab-webscraper-linux-x64.tar.gz -C $installdir
rm $installdir/wordslab-webscraper-linux-x64.tar.gz
cd $installdir
./wordslab-webscraper
```

### MacOS

```
installdir=$HOME/wordslab-webscraper
mkdir $installdir
curl -L -o $installdir/wordslab-webscraper-osx-x64.tar.gz https://github.com/wordslab-org/wordslab-webscraper/releases/download/v1.0.0/wordslab-webscraper-osx-x64.tar.gz
tar -xf $installdir/wordslab-webscraper-osx-x64.tar.gz -C $installdir
rm $installdir/wordslab-webscraper-osx-x64.tar.gz
cd $installdir
./wordslab-webscraper
```

## Usage

Crawls all the Html pages and PDF documents of a website and converts them to text documents.

All the extracted text documents are stored under a single directory named like the website.

The extracted file formats are described below, see "Output file formats".

Features an advanced Html to text conversion algorithm :

- tries to recover the logical structure of the document from the Html layout
- interprets Css properties of the Html nodes to make this operation more reliable
- preserves document / section / list / table grouping and nesting information

Usage to launch a website extraction:

```
wordslab-webscraper [scope] [rootUrl] [key=value optional params]
```

 - scope            : domain | subdomain | path
                      > decide what part of the rootUrl should be used to limit the extraction
 - rootUrl          : root Url of the website (or subfolder of a website) you want to crawl

Usage to continue or restart after a first try:

```
wordslab-webscraper [continue|restart] [rootUrl] [key=value optional params to override]
```

Optional parameters :

 - minCrawlDelay=100   : delay in milliseconds between two requests sent to the website
 - excludeUrls=/*.php$ : stop extracting text from urls starting with this pattern

Optional stopping conditions (the first to be met will stop the crawl, 0 means no limit) :

 - maxDuration=0     : maximum duration of the extraction in minutes
 - maxPageCount=0    : maximum number of pages extracted from the website
 - maxErrorsCount=10 : maximum number of errors during the extraction
 - minUniqueText=5   : minimum percentage of unique text blocks extracted
 - maxSizeOnDisk=0   : maximum size of the extracted text files on disk in Mb

Usage to aggregate all extraction results in a HuggingFace dataset:

```
wordslab-webscraper dataset [domain] [language] [yearMonth]
```

Mandatory parameters :

 - domain    : single word describing the business of knowledge domain of the dataset (ex: 'business')");
 - language  : 2 letters code to filter the dataset language ('fr' for french)");
 - yearMonth : 4 digits giving the year and the month of the web extraction ('2311' for nov 2023)");

Recommended process :

0. Navigate to the rootUrl in your browser and check the links on the page to select a scope for the extraction");
1. *Run* the the tool with the default min crawl delay = 100 ms, until the extraction is stopped after 10 errors");
2. Open the log file \"_wordslab/requests.log.csv\" created in the storageDirectory for the website");
3. Check for Http \"Forbidden\" answers or connection errors, and test if the url is accessible from your browser");
4. *Restart* the extraction with a bigger minCrawlDelay, and continue to increase it until \"Forbidden\" errors disappear");
5. Run the the tool with the default min percentage of unique text = 5% until the extraction is stopped by this criteria");
6. *Continue* the extraction with an additional excludeUrls=... parameter until you get more unique text blocks");
            
The extraction can take a while :

- your system can go to hibernation mode and resume without interrupting the crawl
- your can even stop the crawl (Ctrl-C or shutdown) and continue it later where you left it
- the continue command will use checkpoint and config files found in the "_wordslab" subfolder
- the restart command will ignore any checkpoint, start again at the root url, and overwrite everything

After you finish all websites extractions, use the 'wordslab-webscraper dataset' command to generate a dataset.

Share the dataset on the HuggingFace hub

```
pip install --upgrade huggingface_hub
apt update && apt-get install git-lfs
git lfs install

git config --global credential.helper store
git config --global init.defaultBranch main
git config --global user.email "[your@email]"
git config --global user.name "[your_name]"

# Copy a WRITE access token from your HuggingFace account: https://huggingface.co/settings/tokens
# When prompted "Add token as git credential?" => Yes
huggingface-cli login
huggingface-cli repo create [your_dataset_name] --type dataset

cd [your_dataset_name]
git init
git remote add origin https://huggingface.co/datasets/[your_huggingface_user]/[your_dataset_name].git
git pull origin main

git lfs track *.parquet
git add --all
git commit -m "Add dataset files"
git push --set-upstream origin main
```

## Motivation

**Natural Language Processing** applications based on modern deep learning algorithms often rely on transfer learning techniques which require to pre-train a model on a large corpus of text.

To achieve good results, the corpus of text used in this pre-training step should be as close as possible to the specific inputs your NLP application will receive in production
- regional language
- business or knowledge domain
- document structure and sentence length
- grammar and spelling mistakes
- internal jargon and acronyms

While pre-trained networks for popular languages are often published jointly with the latest algorithms, they are almost always trained on generic Wikipedia or newspapers articles.

This approach is highly inefficient for several reasons
- the network has to learn a lot of knowledge domains not relevant for your application
- the network is trained to process well constructed sentences you will rarely encounter in real life
- entire families of specific business terms are never seen during pre-training
- the pre-processing steps chosen by the author may not be optimal for your specific task

To build an effective NLP application, you often need to **extract a business-domain specific corpus of text from real world documents** such as
- HTML pages from public web sites
- PDF or Word documents from entreprise repositories
- selected Wikipedia articles

The goals of this project are to provide
- a **standard format for text documents** used as input for NLP algorithms
- a **multiplatform tool to extract such text documents from popular sources** listed above 
- an **efficient process to collect text documents** related to your business domain
- a **text pre-processing library** based on the standard document format

The recommended approach is to
- First build a text corpus specific to your NLP application but independent of any algorithm
- Then use the pre-processing library to adapt this corpus to the format expected by a selected algorithm

## Standard Text Document Format

### Design principles

The current NLP algorithms don't know how to use the spatial position of a fragment text on a page, or the text decorations like colors, fonts, size, boldness, links, images ... They model a document as a stream of characters or words.

However, they rely on two important characteristics of the document structure
- The **relative order** of words and text blocks (along two axes in tables)
- A **hierarchical grouping** of text blocks (to compute higher level representations)

The goals of the Standard Text Document Format are to
- Preserve the relative order and hierarchical grouping of text blocks
- Remove every other text positioning and decoration information
- Keep full fidelity of the original character set, punctuation, line breaks
- Avoid any premature pre-processing or sentence segmentation operations
- Produce a human readable and easy to parse text document

### Key concepts

**NLPTextDocument** is a tree of *DocumentElement*s with three properties
- Title (string) : title of the document
- Uri (string) : universal resource identifier of the source document
- Timestamp (DateTime) : last modification date of the source document
- Metadata (Dictionary<string,string>) : optional key / value pairs used to describe the document

There are 4 families of *DocumentElement*s
   - **TextBlock** contains a single consistent block of text, a paragraph for example
   - **Section** contains a list of *DocumentElement*s, with an optional Title property
   - **List** and **NavigationList** contains a list of **ListItem**s, with an optional Title property
     - each ListItem is a list of *DocumentElement*s
   - **Table** contains a list of **TableHeader**s and **TableCell**s, with an optional Title property
     - each *TableElement* has Row/RowSpan and Col/ColSpan properties (position in a 2D array)
     - each *TableElement* is a list of *DocumentElement*s

Each DocumentElement has an integer **NestingLevel** property which tracks the depth of the element in the document tree.
Direct children of NLPTextDocument have a NestingLevel equal to 1.

### Output file formats

The file encoding is **UTF-8** with BOM for all file formats.

Three types of files are generated for each HTML page or PDF document extracted from the source website:

1. Dataframe CSV file: **.[language].dataframe.csv**
  - the column separator is ; and text fields are surrounded by "
  - the columns are: DocEltType; DocEltCmd; NestingLevel; Text; Lang; Chars; Words; AvgWordsLength; LetterChars; NumberChars; OtherChars; HashCode; IsUnique

2. HTML preview file: **.[language].preview.html**

3. Markdown text file: **.[language].text.md**

