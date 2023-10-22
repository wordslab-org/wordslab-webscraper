﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace wordslab.nlptextdoc
{
    public enum DocumentElementType
    {
        Section,
        NavigationList,
        List,
        ListItem,
        Table,
        TableHeader,
        TableCell,
        TextBlock
    }

    public abstract class DocumentElement
    {
        public DocumentElement(int nestingLevel, DocumentElementType type)
        {
            NestingLevel = nestingLevel;
            Type = type;
        }

        public int NestingLevel { get; private set; }
        public DocumentElementType Type { get; private set; }

        public abstract bool IsEmpty { get; }
    }

    public class TextBlock : DocumentElement
    {
        public TextBlock(int nestingLevel, string text)
            : base(nestingLevel, DocumentElementType.TextBlock)
        {
            Text = text;
        }

        public string Text { get; private set; }

        public NLPTextProperties TextProperties { get; set; }

        public override bool IsEmpty => String.IsNullOrEmpty(Text);
    }

    public abstract class GroupElement : DocumentElement
    {
        public GroupElement(int nestingLevel, DocumentElementType type)
            : base(nestingLevel, type)
        {
            Elements = new List<DocumentElement>();
        }

        public IList<DocumentElement> Elements { get; private set; }

        /// <summary>
        /// True if the document tree below this groups of document elements contains at least one unique text block.
        /// Only available after a full text analysis of the document.
        /// </summary>
        public bool ContainsUniqueText { get; set; } = false;

        /// <summary>
        /// Content of the document: first level elements filtered ignoring non unique group elements
        /// </summary>
        public IEnumerable<DocumentElement> UniqueElements
        {
            get
            {
                foreach (var element in Elements)
                {
                    if (element.Type == DocumentElementType.TextBlock)
                    {
                        yield return element;
                    }
                    else if (element is GroupElement)
                    {
                        var groupElement = (GroupElement)element;
                        // We exclude a complete Section, List or Table
                        // but we never exclude a single ListItem or TableElement if the parent container contains unique text
                        if (!(groupElement is GroupElementWithTitle) ||  groupElement.ContainsUniqueText)
                        {
                            yield return groupElement;
                        }
                    }
                }
            }
        }

        public override bool IsEmpty => Elements.Count == 0 || !UniqueElements.Any(elt => !elt.IsEmpty);
    }

    public abstract class GroupElementWithTitle : GroupElement
    {
        public GroupElementWithTitle(int nestingLevel, DocumentElementType type, string title)
            : base(nestingLevel, type)
        {
            Title = title;
        }

        public string Title { get; set; }

        public NLPTextProperties TitleProperties { get; set; }

        public bool HasTitle => !String.IsNullOrEmpty(Title);
    }

    public class Section : GroupElementWithTitle
    {
        public Section(int nestingLevel)
            : this(nestingLevel, null)
        { }

        public Section(int nestingLevel, string title)
            : base(nestingLevel, DocumentElementType.Section, title)
        { }
    }

    public class List : GroupElementWithTitle
    {
        public bool IsNavigationList { get { return Type == DocumentElementType.NavigationList; } }

        public List(int nestingLevel)
            : this(nestingLevel, false, null)
        { }

        public List(int nestingLevel, bool isNavigationList)
            : this(nestingLevel, isNavigationList, null)
        { }

        public List(int nestingLevel, string title)
            : this(nestingLevel, false, null)
        { }

        public List(int nestingLevel, bool isNavigationList, string title)
            : base(nestingLevel, isNavigationList ? DocumentElementType.NavigationList : DocumentElementType.List, title)
        { }
    }

    public class ListItem : GroupElement
    {
        public ListItem(int nestingLevel)
            : base(nestingLevel, DocumentElementType.ListItem)
        { }
    }

    public class Table : GroupElementWithTitle
    {
        public Table(int nestingLevel)
            : this(nestingLevel, null)
        { }

        public Table(int nestingLevel, string title)
            : base(nestingLevel, DocumentElementType.Table, title)
        { }
    }

    public class TableElement : GroupElement
    {
        public TableElement(int nestingLevel, DocumentElementType type, int row, int col)
            : this(nestingLevel, type, row, 1, col, 1)
        { }

        public TableElement(int nestingLevel, DocumentElementType type, int row, int rowspan, int col, int colspan)
            : base(nestingLevel, type)
        {
            Row = row;
            RowSpan = rowspan;
            Col = col;
            ColSpan = colspan;
        }

        public int Row { get; private set; }
        public int RowSpan { get; private set; }

        public int Col { get; private set; }
        public int ColSpan { get; private set; }
    }

    public class TableHeader : TableElement
    {
        public TableHeader(int nestingLevel, int row, int col)
            : base(nestingLevel, DocumentElementType.TableHeader, row, col)
        { }

        public TableHeader(int nestingLevel, int row, int rowspan, int col, int colspan)
            : base(nestingLevel, DocumentElementType.TableHeader, row, rowspan, col, colspan)
        { }
    }

    public class TableCell : TableElement
    {
        public TableCell(int nestingLevel, int row, int col)
            : base(nestingLevel, DocumentElementType.TableCell, row, col)
        { }

        public TableCell(int nestingLevel, int row, int rowspan, int col, int colspan)
            : base(nestingLevel, DocumentElementType.TableCell, row, rowspan, col, colspan)
        { }
    }
}
