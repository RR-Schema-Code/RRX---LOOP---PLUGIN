using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Media;
using System.Xml.Linq;

namespace RRX.Xml.ProjectTree
{
    public enum TemplateTreeItemKind
    {
        Group,
        Template,
        PageTemplate
    }

    public class TemplateTreeItem : INotifyPropertyChanged, IItemProjectTree
    {
        private string name = string.Empty;
        private string xmlTemplate = string.Empty;
        private string article = string.Empty;
        private string sheetName = string.Empty;
        private string pageTemplateName = string.Empty;
        private string columnNeed = string.Empty;
        private string columnSpacing = string.Empty;
        private int priority = 100;
        private bool singlePage;
        private bool isExpanded = true;

        public TemplateTreeItem()
        {
            Children.CollectionChanged += Children_CollectionChanged;
        }

        public TemplateTreeItemKind Kind { get; set; }
        public bool IsTemplate => Kind == TemplateTreeItemKind.Template;
        public bool IsPageTemplate => Kind == TemplateTreeItemKind.PageTemplate;
        public bool HasXmlTemplate => IsTemplate || IsPageTemplate;
        public bool HasPriority => IsTemplate || IsPageTemplate;

        public string Name
        {
            get => name;
            set
            {
                name = value;
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(Detail));
            }
        }

        public string XmlTemplate
        {
            get => xmlTemplate;
            set
            {
                xmlTemplate = value;
                OnPropertyChanged(nameof(XmlTemplate));
            }
        }

        public string Article
        {
            get => article;
            set
            {
                article = value;
                OnPropertyChanged(nameof(Article));
                OnPropertyChanged(nameof(Detail));
            }
        }

        public string SheetName
        {
            get => sheetName;
            set
            {
                sheetName = value;
                OnPropertyChanged(nameof(SheetName));
            }
        }

        public string PageTemplateName
        {
            get => pageTemplateName;
            set
            {
                pageTemplateName = value;
                OnPropertyChanged(nameof(PageTemplateName));
            }
        }

        public string ColumnNeed
        {
            get => columnNeed;
            set
            {
                columnNeed = value;
                OnPropertyChanged(nameof(ColumnNeed));
            }
        }

        public string ColumnSpacing
        {
            get => columnSpacing;
            set
            {
                columnSpacing = value;
                OnPropertyChanged(nameof(ColumnSpacing));
            }
        }

        public int Priority
        {
            get => priority;
            set
            {
                priority = value;
                OnPropertyChanged(nameof(Priority));
                OnPropertyChanged(nameof(Detail));
            }
        }

        public bool SinglePage
        {
            get => singlePage;
            set
            {
                singlePage = value;
                OnPropertyChanged(nameof(SinglePage));
            }
        }

        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }

        public string DisplayName => string.IsNullOrWhiteSpace(Name)
            ? Kind switch
            {
                TemplateTreeItemKind.Group => "Neue Gruppe",
                TemplateTreeItemKind.PageTemplate => "Neues Seitentemplate",
                _ => "Neue Vorlage"
            }
            : Name;

        public string Detail => Kind switch
        {
            TemplateTreeItemKind.Template => Article,
            TemplateTreeItemKind.PageTemplate => $"Priorität {Priority}",
            _ => $"{Children.Count} Vorlage(n)"
        };

        public Brush IconBrush => Kind == TemplateTreeItemKind.Group ? Brushes.Gray : Brushes.DimGray;

        public string IconData => Kind == TemplateTreeItemKind.Group
            ? "M 8.5 8 C 6.019 8 4 10.019 4 12.5 L 4 18 L 16.052734 18 C 16.636734 18 17.202344 17.793922 17.652344 17.419922 L 23.5 12.546875 L 19.572266 9.2734375 C 18.586266 8.4524375 17.336734 8 16.052734 8 L 8.5 8 z M 27.644531 13 L 19.572266 19.724609 C 18.585266 20.546609 17.336734 21 16.052734 21 L 4 21 L 4 35.5 C 4 37.981 6.019 40 8.5 40 L 39.5 40 C 41.981 40 44 37.981 44 35.5 L 44 17.5 C 44 15.019 41.981 13 39.5 13 L 27.644531 13 z"
            : "M 6 3 A 1.0001 1.0001 0 1 0 6 5 L 6 8 L 10 8 L 10 5 A 1.0001 1.0001 0 1 0 10 3 L 6 3 z M 16.828125 7 C 16.298125 7 15.789062 7.2109375 15.414062 7.5859375 L 13 10 L 13 22 L 15.414062 24.414062 C 15.789062 24.789062 16.298125 25 16.828125 25 L 28 25 C 29.105 25 30 24.105 30 23 L 30 9 C 30 7.895 29.105 7 28 7 L 16.828125 7 z M 19 9 L 27 9 C 27.553 9 28 9.447 28 10 L 28 22 C 28 22.553 27.553 23 27 23 L 19 23 C 18.447 23 18 22.553 18 22 C 18 21.447 18.447 21 19 21 L 26 21 L 26 19 L 19 19 C 18.447 19 18 18.553 18 18 C 18 17.447 18.447 17 19 17 L 26 17 L 26 15 L 19 15 C 18.447 15 18 14.553 18 14 C 18 13.447 18.447 13 19 13 L 26 13 L 26 11 L 19 11 C 18.447 11 18 10.553 18 10 C 18 9.447 18.447 9 19 9 z M 6 10 C 4.895 10 4 10.895 4 12 L 4 13 L 2 13 A 1.0001 1.0001 0 0 0 0.984375 11.986328 A 1.0001 1.0001 0 0 0 0 13 L 0 19 A 1.0001 1.0001 0 1 0 2 19 L 4 19 L 4 20 C 4 21.105 4.895 22 6 22 L 11 22 L 11 10 L 6 10 z";

        public ObservableCollection<IItemProjectTree> Children { get; set; } = new();

        public IItemProjectTree Copy()
        {
            var copy = new TemplateTreeItem
            {
                Kind = Kind,
                Name = Name,
                XmlTemplate = XmlTemplate,
                Article = Article,
                SheetName = SheetName,
                PageTemplateName = PageTemplateName,
                ColumnNeed = ColumnNeed,
                ColumnSpacing = ColumnSpacing,
                Priority = Priority,
                SinglePage = SinglePage,
                IsExpanded = IsExpanded
            };

            foreach (var child in Children)
            {
                copy.Children.Add(child.Copy());
            }

            return copy;
        }

        public XElement Xml()
        {
            var element = new XElement(Kind switch
            {
                TemplateTreeItemKind.Group => "group",
                TemplateTreeItemKind.PageTemplate => "page-template",
                _ => "template"
            });
            element.SetAttributeValue("name", Name);
            element.SetAttributeValue("xml-template", XmlTemplate);
            element.SetAttributeValue("artikel", Article);
            element.SetAttributeValue("normblatt", SheetName);
            element.SetAttributeValue("seitentemplate", PageTemplateName);
            element.SetAttributeValue("spalten-bedarf", ColumnNeed);
            element.SetAttributeValue("spalten-abstand", ColumnSpacing);
            element.SetAttributeValue("prioritaet", Priority);
            element.SetAttributeValue("single-page", SinglePage);

            foreach (var child in Children)
            {
                element.Add(child.Xml());
            }

            return element;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Children_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Detail));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
