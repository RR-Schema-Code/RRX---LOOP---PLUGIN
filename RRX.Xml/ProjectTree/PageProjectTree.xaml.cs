using Microsoft.Win32;
using RRX.DragDrop;
using RRX.DragDrop.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace RRX.Xml.ProjectTree
{
    public partial class PageProjectTree : Page, INotifyPropertyChanged, IDropTarget
    {
        public const string BuildVersion = "RRX XML 2026-09-20.001";
        private const string DefaultSheetName = "~A4 - Loop - Default";
        private static readonly IComparer<string> NaturalKeyComparer = new NaturalStringComparer();
        private readonly ItemProjectTreeDefault rrxRoot = new();
        private XDocument? rrxDocument;
        private string? rrxFilePath;
        private string? templateTreeFilePath;
        private TemplateTreeItem? selectedTemplateItem;

        public ObservableCollection<IItemProjectTree> TemplateItems { get; } = new();

        public TemplateTreeItem? SelectedTemplateItem
        {
            get => selectedTemplateItem;
            set
            {
                selectedTemplateItem = value;
                OnPropertyChanged(nameof(SelectedTemplateItem));
            }
        }

        public PageProjectTree()
        {
            InitializeComponent();
            DataContext = this;
            VersionText.Text = BuildVersion;

            LoadInitialRrxTree();
            RefreshTemplateStatus();
        }

        private void LoadInitialRrxTree()
        {
            var workspaceTemplate = FindFileInParentDirectories("46d3b839-c96a-42ea-820e-e069499cb947.tmp");
            if (!string.IsNullOrWhiteSpace(workspaceTemplate))
            {
                LoadRrxXml(workspaceTemplate);
                return;
            }

            RrxStatus.Text = "Keine XML geladen";
            RefreshTreeSource();
        }

        private static string? FindFileInParentDirectories(string fileName)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return null;
        }

        private void LoadRrxXml(string filePath)
        {
            rrxFilePath = filePath;
            rrxDocument = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);

            rrxRoot.Children.Clear();

            var rootItem = new RrxProjectTreeItem
            {
                Kind = RrxProjectTreeItemKind.Root,
                SourceSide = "RRX",
                Name = Path.GetFileName(filePath),
                Label = "RRX XML",
                IsExpanded = true
            };
            rrxRoot.Children.Add(rootItem);

            foreach (var aspGroup in rrxDocument.Descendants("cmp").GroupBy(ReadAspKey))
            {
                var firstAspComponent = aspGroup.First();
                var aspItem = new RrxProjectTreeItem
                {
                    Kind = RrxProjectTreeItemKind.Asp,
                    SourceSide = "RRX",
                    Key = aspGroup.Key,
                    Label = AttributeValue(firstAspComponent, "g.asp.lb"),
                    Name = aspGroup.Key,
                    IsExpanded = true
                };
                rootItem.Children.Add(aspItem);

                foreach (var systemGroup in aspGroup.GroupBy(ReadSystemKey))
                {
                    var firstSystemComponent = systemGroup.First();
                    var systemItem = new RrxProjectTreeItem
                    {
                        Kind = RrxProjectTreeItemKind.SystemFolder,
                        SourceSide = "RRX",
                        Key = systemGroup.Key,
                        Label = AttributeValue(firstSystemComponent, "g.sys.lb").Trim(),
                        Name = systemGroup.Key
                    };
                    aspItem.Children.Add(systemItem);

                    foreach (var component in systemGroup.OrderBy(ReadComponentKey, NaturalKeyComparer).ThenBy(ReadSequence))
                    {
                        systemItem.Children.Add(CreateComponentItem(component));
                    }
                }
            }

            RrxStatus.Text = $"{rrxDocument.Descendants("cmp").Count()} Komponente(n) geladen: {Path.GetFileName(filePath)}";
            RefreshTreeSource();
        }

        private static RrxProjectTreeItem CreateComponentItem(XElement component)
        {
            var item = new RrxProjectTreeItem
            {
                Kind = RrxProjectTreeItemKind.Component,
                SourceElement = component,
                SourceSide = "RRX",
                Key = ReadComponentKey(component),
                Label = AttributeValue(component, "g.lb"),
                Name = AttributeValue(component, "fnc"),
                Uid = ReadComponentUid(component)
            };

            AddChildren(item, component, "dpt", RrxProjectTreeItemKind.DataPoint);
            AddChildren(item, component, "prt", RrxProjectTreeItemKind.Port);
            AddChildren(item, component, "wir", RrxProjectTreeItemKind.Wire);
            AddChildren(item, component, "art", RrxProjectTreeItemKind.Article);
            AddChildren(item, component, "add", RrxProjectTreeItemKind.Additional);

            return item;
        }

        private static void AddChildren(RrxProjectTreeItem parent, XElement component, string elementName, RrxProjectTreeItemKind kind)
        {
            foreach (var child in component.Elements(elementName))
            {
                parent.Children.Add(new RrxProjectTreeItem
                {
                    Kind = kind,
                    SourceElement = child,
                    SourceSide = parent.SourceSide,
                    Key = AttributeValue(child, "g.ky", AttributeValue(child, "key", AttributeValue(child, "nam"))),
                    Label = kind == RrxProjectTreeItemKind.Port
                        ? AttributeValue(child, "uds")
                        : AttributeValue(child, "g.lb", AttributeValue(child, "dsc", AttributeValue(child, "fnc"))),
                    Uid = AttributeValue(child, "uid"),
                    Name = child.Name.LocalName
                });
            }
        }

        private void RefreshTreeSource()
        {
            if (Templates.ItemsSource == null)
            {
                Templates.ItemsSource = rrxRoot.Children;
            }
        }

        private static string ReadAspKey(XElement component)
        {
            return AttributeValue(component, "g.asp.ky", "Ohne ASP");
        }

        private static string ReadSystemKey(XElement component)
        {
            return AttributeValue(component, "g.sys.ky", "Ohne System");
        }

        private static string ReadSystemName(XElement component)
        {
            var systemName = AttributeValue(component, "g.sys.nam");
            if (!string.IsNullOrWhiteSpace(systemName))
            {
                return systemName;
            }

            var importedFolderName = component.Ancestors("syf")
                .Select(folder => AttributeValue(folder, "nam"))
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
            if (!string.IsNullOrWhiteSpace(importedFolderName))
            {
                return importedFolderName;
            }

            var systemKey = ReadSystemKey(component);
            var separatorIndex = systemKey.LastIndexOf('-');
            return separatorIndex >= 0 && separatorIndex < systemKey.Length - 1
                ? systemKey[(separatorIndex + 1)..]
                : systemKey;
        }

        private static string ReadSystemDescription(XElement component)
        {
            return AttributeValue(component, "g.sys.des", AttributeValue(component, "g.sys.lb", ReadSystemKey(component))).Trim();
        }

        private static string ReadComponentKey(XElement component)
        {
            return AttributeValue(component, "g.ky", AttributeValue(component, "key", component.Name.LocalName));
        }

        private static string ReadComponentLabel(XElement component)
        {
            return AttributeValue(component, "g.lbl",
                AttributeValue(component, "g.lb",
                    AttributeValue(component, "lbl",
                        AttributeValue(component, "label", ReadComponentKey(component)))));
        }

        private static int ReadSequence(XElement component)
        {
            return int.TryParse(AttributeValue(component, "seq"), out var sequence) ? sequence : int.MaxValue;
        }

        private static string AttributeValue(XElement element, string name, string fallback = "")
        {
            return element.Attribute(name)?.Value ?? fallback;
        }

        private static string ReadComponentUid(XElement component)
        {
            return AttributeValue(component, "uid", AttributeValue(component, "bid"));
        }

        private void Tree_Loaded(object sender, RoutedEventArgs e)
        {
            var presenter = (sender as TreeView)?.GetVisualDescendent<ScrollContentPresenter>();
            if (presenter != null)
            {
                presenter.RequestBringIntoView -= Tree_RequestBringIntoView;
                presenter.RequestBringIntoView += Tree_RequestBringIntoView;
            }
        }

        private void Tree_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }

        private void TreeItem_Collapsed(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(sender, e.OriginalSource) && sender is TreeViewItem treeViewItem)
            {
                CollapseChildItems(treeViewItem);
            }
        }

        private static void CollapseChildItems(ItemsControl parent)
        {
            parent.ApplyTemplate();
            var itemsPresenter = parent.GetVisualDescendent<ItemsPresenter>();
            itemsPresenter?.ApplyTemplate();

            foreach (var item in parent.Items)
            {
                if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem child)
                {
                    child.IsExpanded = false;
                    CollapseChildItems(child);
                }
            }
        }

        private void LoadRrx_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "RRX XML (*.xml;*.rrx;*.tmp)|*.xml;*.rrx;*.tmp|Alle Dateien (*.*)|*.*"
            };

            if (!string.IsNullOrWhiteSpace(rrxFilePath))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(rrxFilePath);
                dialog.FileName = Path.GetFileName(rrxFilePath);
            }

            if (dialog.ShowDialog() == true)
            {
                LoadRrxXml(dialog.FileName);
            }
        }

        private void GenerateOutput_Click(object sender, RoutedEventArgs e)
        {
            if (rrxDocument == null)
            {
                OutputXml.Text = string.Empty;
                RrxStatus.Text = "Bitte zuerst eine XML laden";
                return;
            }

            var templateMap = BuildTemplateArticleMap();
            var pageTemplates = BuildPageTemplates();
            if (templateMap.Count == 0)
            {
                OutputXml.Text = string.Empty;
                TemplateStatus.Text = "Keine Vorlagenartikel zum Abgleichen vorhanden";
                return;
            }

            var systemGroups = rrxDocument
                .Descendants("cmp")
                .GroupBy(ReadSystemKey)
                .Select(group => new
                {
                    Components = group.ToList(),
                    MatchedComponents = MatchSystemComponents(group, templateMap).ToList()
                })
                .Where(group => group.MatchedComponents.Count > 0)
                .ToList();

            if (systemGroups.Count == 0)
            {
                OutputXml.Text = string.Empty;
                RrxStatus.Text = "Kein Betriebsmittel mit passender Artikel-Vorlage gefunden";
                return;
            }

            var referenceContext = new LoopReferenceContext();
            PrimeModuleReferences(systemGroups.SelectMany(group => group.MatchedComponents), referenceContext);

            var syfElements = systemGroups
                .Select((group, index) => CreateSystemFolderOutput(group.Components.First(), group.MatchedComponents, pageTemplates, referenceContext, index + 1))
                .Select(element => element.ToString());

            OutputXml.Text = string.Join(Environment.NewLine, syfElements);
            RrxStatus.Text = $"{systemGroups.Count} Systemfolder-Ausgabe(n) generiert";
        }

        private static XElement CreateSystemFolderOutput(XElement firstSystemComponent, IReadOnlyList<MatchedComponent> matchedComponents, IReadOnlyList<TemplateTreeItem> pageTemplates, LoopReferenceContext referenceContext, int folderGroup)
        {
            var pages = BuildSystemPages(matchedComponents);
            var syf = new XElement("syf",
                new XAttribute("uid", Guid.NewGuid().ToString()),
                new XAttribute("nam", ReadSystemName(firstSystemComponent)),
                new XAttribute("des", ReadSystemDescription(firstSystemComponent)),
                new XAttribute("key", ReadSystemKey(firstSystemComponent)),
                new XAttribute("mrk", string.Empty),
                new XAttribute("prt", "1"),
                new XAttribute("mac", string.Empty),
                new XAttribute("ink", string.Empty),
                new XAttribute("ind", string.Empty),
                new XAttribute("trk", string.Empty),
                new XAttribute("trd", string.Empty),
                new XAttribute("tyk", string.Empty),
                new XAttribute("tyd", string.Empty),
                new XAttribute("fgr", folderGroup.ToString(CultureInfo.InvariantCulture)),
                new XText(Environment.NewLine + "\t"));

            for (var index = 0; index < pages.Count; index++)
            {
                syf.Add(CreateSystemPage(pages[index], pageTemplates, referenceContext));
            }

            return syf;
        }

        private static XElement CreateSystemPage(IReadOnlyList<MatchedComponent> pageComponents, IReadOnlyList<TemplateTreeItem> pageTemplates, LoopReferenceContext referenceContext)
        {
            var pageName = string.Join("; ", pageComponents
                .Select(item => AttributeValue(item.Component, "fnc"))
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            var sheetName = ResolvePageSheetName(pageComponents);

            var page = new XElement("lpg",
                new XAttribute("uid", Guid.NewGuid().ToString()),
                new XAttribute("nam", pageName),
                new XAttribute("des", string.Empty),
                new XAttribute("mrk", string.Empty),
                new XAttribute("prt", "1"),
                new XAttribute("sht", sheetName),
                new XElement("x.pag",
                    new XAttribute("nm", string.Empty),
                    new XAttribute("snp", "5"),
                    new XAttribute("scl", "1"),
                    new XAttribute("hgt", "100"),
                    new XAttribute("wdt", "100"),
                    new XAttribute("unt", "1"),
                    new XAttribute("bkg", "sc:#FFFFFFFF"),
                    new XAttribute("fpw", string.Empty)));

            foreach (var element in CreatePageTemplateElements(pageComponents, pageTemplates))
            {
                page.Add(element);
            }

            foreach (var element in CreatePageTemplateElements(pageComponents, referenceContext))
            {
                page.Add(element);
            }

            return page;
        }

        private static string ResolvePageSheetName(IReadOnlyList<MatchedComponent> pageComponents)
        {
            var firstSheetName = pageComponents.FirstOrDefault()?.Template.SheetName;
            return string.IsNullOrWhiteSpace(firstSheetName) ? DefaultSheetName : firstSheetName;
        }

        private static IReadOnlyList<IReadOnlyList<MatchedComponent>> BuildSystemPages(IReadOnlyList<MatchedComponent> matchedComponents)
        {
            const double defaultTemplateWidth = 144.0;
            const double pageWidth = 1440.0;
            var pages = new List<IReadOnlyList<MatchedComponent>>();
            var currentPage = new List<MatchedComponent>();
            var currentWidth = 0.0;

            foreach (var matchedComponent in matchedComponents)
            {
                var requiredWidth = matchedComponent.Template.SinglePage
                    ? pageWidth
                    : Math.Max(1.0, ParsePositiveDouble(matchedComponent.Template.ColumnNeed, defaultTemplateWidth)) + Math.Max(0.0, ParsePositiveDouble(matchedComponent.Template.ColumnSpacing, 0.0));

                if (matchedComponent.Template.SinglePage)
                {
                    AddCurrentPage();
                    pages.Add(new[] { matchedComponent });
                    continue;
                }

                if (currentPage.Count > 0 && currentWidth + requiredWidth > pageWidth)
                {
                    AddCurrentPage();
                }

                currentPage.Add(matchedComponent);
                currentWidth += requiredWidth;
            }

            AddCurrentPage();
            return pages;

            void AddCurrentPage()
            {
                if (currentPage.Count == 0)
                {
                    return;
                }

                pages.Add(currentPage.ToList());
                currentPage.Clear();
                currentWidth = 0.0;
            }
        }

        private static IEnumerable<XElement> CreatePageTemplateElements(IReadOnlyList<MatchedComponent> pageComponents, LoopReferenceContext referenceContext)
        {
            const double defaultColumnWidth = 144.0;
            var currentX = 0.0;
            var nextCluster = 1;

            foreach (var matchedComponent in pageComponents)
            {
                var elements = referenceContext.GetPreparedElements(matchedComponent);
                foreach (var element in elements)
                {
                    ApplyComponentValues(element, matchedComponent.Component, referenceContext);
                }
                AssignPageClusters(elements, ref nextCluster);

                if (matchedComponent.Template.SinglePage)
                {
                    foreach (var element in elements)
                    {
                        yield return element;
                    }

                    continue;
                }

                var spacingPx = Math.Max(0.0, ParsePositiveDouble(matchedComponent.Template.ColumnSpacing, 0.0));
                var neededPx = Math.Max(1.0, ParsePositiveDouble(matchedComponent.Template.ColumnNeed, defaultColumnWidth));
                currentX += spacingPx;

                foreach (var element in elements)
                {
                    MoveTemplateElement(element, currentX);
                    yield return element;
                }

                currentX += neededPx;
            }
        }

        private static IEnumerable<XElement> CreatePageTemplateElements(IReadOnlyList<MatchedComponent> pageComponents, IReadOnlyList<TemplateTreeItem> pageTemplates)
        {
            var pageTemplate = ResolvePageTemplate(pageComponents, pageTemplates);
            if (pageTemplate == null)
            {
                yield break;
            }

            var elements = LoadTemplateElements(pageTemplate.XmlTemplate)
                .Where(element => element.Name.LocalName != "x.pag")
                .ToList();
            RegenerateTemplateIds(elements);

            foreach (var element in elements)
            {
                yield return element;
            }
        }

        private static TemplateTreeItem? ResolvePageTemplate(IReadOnlyList<MatchedComponent> pageComponents, IReadOnlyList<TemplateTreeItem> pageTemplates)
        {
            var pageTemplatesByName = pageTemplates
                .Where(template => !string.IsNullOrWhiteSpace(template.Name))
                .GroupBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.OrderBy(template => template.Priority).First(), StringComparer.OrdinalIgnoreCase);

            return pageComponents
                .Select(component => component.Template.PageTemplateName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => pageTemplatesByName.TryGetValue(name, out var pageTemplate) ? pageTemplate : null)
                .Where(pageTemplate => pageTemplate != null)
                .OrderBy(pageTemplate => pageTemplate!.Priority)
                .FirstOrDefault();
        }

        private static IEnumerable<XElement> LoadTemplateElements(string xmlTemplate)
        {
            if (string.IsNullOrWhiteSpace(xmlTemplate))
            {
                return Enumerable.Empty<XElement>();
            }

            try
            {
                var document = File.Exists(xmlTemplate)
                    ? XDocument.Load(xmlTemplate, LoadOptions.PreserveWhitespace)
                    : XDocument.Parse(xmlTemplate, LoadOptions.PreserveWhitespace);

                return document.Root?.Elements().Select(element => new XElement(element)).ToList()
                    ?? Enumerable.Empty<XElement>();
            }
            catch
            {
                return Enumerable.Empty<XElement>();
            }
        }

        private static void PrimeModuleReferences(IEnumerable<MatchedComponent> matchedComponents, LoopReferenceContext referenceContext)
        {
            foreach (var matchedComponent in matchedComponents)
            {
                var elements = LoadTemplateElements(matchedComponent.Template.XmlTemplate).ToList();
                RegenerateTemplateIds(elements);
                referenceContext.AddPreparedElements(matchedComponent, elements);
                foreach (var module in elements.SelectMany(element => element.DescendantsAndSelf("x.lpm")))
                {
                    ApplyModuleValues(module, matchedComponent.Component, referenceContext);
                }
            }
        }

        private static void ApplyComponentValues(XElement element, XElement component, LoopReferenceContext referenceContext)
        {
            if (element.Name.LocalName == "x.lpcmp")
            {
                ApplyLoopComponentValues(element, component);
            }
            else if (element.Name.LocalName == "x.lpm")
            {
                ApplyModuleValues(element, component, referenceContext);
            }
            foreach (var child in element.Elements())
            {
                ApplyComponentValues(child, component, referenceContext);
            }
        }

        private static void ApplyLoopComponentValues(XElement element, XElement component)
        {
            element.SetAttributeValue("lky", ReadComponentKey(component));
        }

        private static void ApplyModuleValues(XElement element, XElement component, LoopReferenceContext referenceContext)
        {
            SetAttributeIfPresent(element, "lky", AttributeValue(component, "key", ReadComponentKey(component)));
            ApplyModulePortValues(element, component);
        }

        private static void ApplyModulePortValues(XElement module, XElement component)
        {
            var datapointComponents = component.Document?.Descendants("cmp") ?? new[] { component };
            var datapointsByUid = datapointComponents.SelectMany(ReadComponentDatapoints)
                .Where(datapoint => !string.IsNullOrWhiteSpace(datapoint.Uid))
                .GroupBy(datapoint => datapoint.Uid, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var componentPorts = component.Elements("prt")
                .Where(port => !string.IsNullOrWhiteSpace(AttributeValue(port, "nam")))
                .GroupBy(port => AttributeValue(port, "nam"), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var port in module.Descendants("x.lpp").Where(port => !string.IsNullOrWhiteSpace(AttributeValue(port, "lnm"))))
            {
                if (!componentPorts.TryGetValue(AttributeValue(port, "lnm"), out var componentPort) ||
                    !datapointsByUid.TryGetValue(AttributeValue(componentPort, "uds"), out var datapoint))
                {
                    continue;
                }

                port.SetAttributeValue("dky", datapoint.Key);
                port.SetAttributeValue("dlb", datapoint.Label);
                port.SetAttributeValue("din", datapoint.Input);
                port.SetAttributeValue("dsg", datapoint.Signal);
                port.SetAttributeValue("dui", datapoint.Unit);
                port.SetAttributeValue("dsc", datapoint.Scale);
                port.SetAttributeValue("drf", datapoint.Uid);
                port.SetAttributeValue("lmk", datapoint.Mark);
            }
        }

        private static void RegenerateTemplateIds(IReadOnlyList<XElement> elements)
        {
            var uidMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var element in elements.SelectMany(element => element.DescendantsAndSelf()))
            {
                var uid = element.Attribute("uid");
                if (uid == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(uid.Value))
                {
                    uid.Value = Guid.NewGuid().ToString();
                    continue;
                }

                if (!uidMap.TryGetValue(uid.Value, out var newUid))
                {
                    newUid = Guid.NewGuid().ToString();
                    uidMap.Add(uid.Value, newUid);
                }

                uid.Value = newUid;
            }

            foreach (var link in elements.SelectMany(element => element.DescendantsAndSelf()).Attributes("lln"))
            {
                if (uidMap.TryGetValue(link.Value, out var newUid))
                {
                    link.Value = newUid;
                }
            }
        }

        private static void AssignPageClusters(IReadOnlyList<XElement> elements, ref int nextCluster)
        {
            var clusterMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var cluster in elements.SelectMany(element => element.DescendantsAndSelf()).Attributes("lcl"))
            {
                if (string.IsNullOrWhiteSpace(cluster.Value))
                {
                    continue;
                }

                if (!clusterMap.TryGetValue(cluster.Value, out var newCluster))
                {
                    newCluster = nextCluster.ToString(CultureInfo.InvariantCulture);
                    nextCluster++;
                    clusterMap.Add(cluster.Value, newCluster);
                }

                cluster.Value = newCluster;
            }
        }

        private static void SetAttributeIfPresent(XElement element, string name, string value)
        {
            if (element.Attribute(name) != null)
            {
                element.SetAttributeValue(name, value);
            }
        }

        private static void MoveTemplateElement(XElement element, double offsetX)
        {
            foreach (var attribute in element.Attributes().ToList())
            {
                if (attribute.Name.LocalName is "pts" or "cdp" or "cip" or "lcp")
                {
                    attribute.Value = OffsetPointAttribute(attribute.Value, offsetX);
                }
            }
        }

        private static string OffsetPointAttribute(string value, double offsetX)
        {
            return Regex.Replace(value, @"(?<x>-?\d+(?:[.,]\d+)?)(?<sep>;)(?<y>-?\d+(?:[.,]\d+)?)", match =>
            {
                var x = ParseDouble(match.Groups["x"].Value);
                if (x == null)
                {
                    return match.Value;
                }

                return $"{FormatDouble(x.Value + offsetX)}{match.Groups["sep"].Value}{match.Groups["y"].Value}";
            });
        }

        private static double? ParseDouble(string value)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue))
            {
                return invariantValue;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.GetCultureInfo("de-DE"), out var germanValue))
            {
                return germanValue;
            }

            return null;
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private Dictionary<string, TemplateTreeItem> BuildTemplateArticleMap()
        {
            var templates = FlattenTemplateItems(TemplateItems)
                .Where(item => item.Kind == TemplateTreeItemKind.Template)
                .ToList();

            var map = new Dictionary<string, TemplateTreeItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var template in templates)
            {
                foreach (var article in SplitArticleValues(template.Article))
                {
                    if (!map.TryGetValue(article, out var existingTemplate) || template.Priority < existingTemplate.Priority)
                    {
                        map[article] = template;
                    }
                }
            }

            return map;
        }

        private IReadOnlyList<TemplateTreeItem> BuildPageTemplates()
        {
            return FlattenTemplateItems(TemplateItems)
                .Where(item => item.Kind == TemplateTreeItemKind.PageTemplate)
                .ToList();
        }

        private static IEnumerable<MatchedComponent> MatchSystemComponents(IEnumerable<XElement> components, IReadOnlyDictionary<string, TemplateTreeItem> templateMap)
        {
            var matches = new List<MatchedComponent>();
            foreach (var component in components)
            {
                foreach (var article in ReadComponentArticles(component))
                {
                    if (templateMap.TryGetValue(article, out var template))
                    {
                        matches.Add(new MatchedComponent(component, template, article));
                        break;
                    }
                }
            }

            return matches
                .OrderBy(match => match.Template.Priority)
                .ThenBy(match => ReadComponentKey(match.Component), NaturalKeyComparer)
                .ThenBy(match => ReadSequence(match.Component));
        }

        private static IEnumerable<string> ReadComponentArticles(XElement component)
        {
            return component
                .Elements("art")
                .SelectMany(article => SplitArticleValues(
                    AttributeValue(article, "g.ky",
                        AttributeValue(article, "key",
                            AttributeValue(article, "nam",
                                AttributeValue(article, "g.lb",
                                AttributeValue(article, "dsc", article.Value)))))));
        }

        private static IEnumerable<ComponentDatapoint> ReadComponentDatapoints(XElement component)
        {
            foreach (var datapoint in component.Elements("dpt"))
            {
                var key = AttributeValue(datapoint, "g.ky",
                    AttributeValue(datapoint, "key",
                        AttributeValue(datapoint, "nam",
                            AttributeValue(datapoint, "dky"))));

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                yield return new ComponentDatapoint(
                    key,
                    AttributeValue(datapoint, "g.lbl",
                        AttributeValue(datapoint, "g.lb",
                            AttributeValue(datapoint, "lbl",
                                AttributeValue(datapoint, "dlb", key)))),
                    AttributeValue(datapoint, "typ",
                        AttributeValue(datapoint, "type",
                            AttributeValue(datapoint, "din"))),
                    AttributeValue(datapoint, "sig",
                        AttributeValue(datapoint, "signal",
                            AttributeValue(datapoint, "dsg"))),
                    AttributeValue(datapoint, "unt",
                        AttributeValue(datapoint, "unit",
                            AttributeValue(datapoint, "dui"))),
                    AttributeValue(datapoint, "scl",
                        AttributeValue(datapoint, "scale",
                            AttributeValue(datapoint, "dsc"))),
                    AttributeValue(datapoint, "uid",
                        AttributeValue(datapoint, "bid",
                            AttributeValue(datapoint, "drf"))),
                    AttributeValue(datapoint, "mrk",
                    AttributeValue(datapoint, "lmk")));
            }
        }

        private static IEnumerable<ComponentWire> ReadComponentWires(XElement component)
        {
            foreach (var wire in component.Elements("wir"))
            {
                var key = AttributeValue(wire, "g.ky",
                    AttributeValue(wire, "key",
                        AttributeValue(wire, "nam",
                            AttributeValue(wire, "lky"))));

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                yield return new ComponentWire(
                    key,
                    AttributeValue(wire, "cnt",
                        AttributeValue(wire, "kct",
                            AttributeValue(wire, "lct"))),
                    AttributeValue(wire, "fnc",
                        AttributeValue(wire, "lfn")),
                    AttributeValue(wire, "typ",
                        AttributeValue(wire, "ltp")),
                    AttributeValue(wire, "gge",
                        AttributeValue(wire, "gauge",
                            AttributeValue(wire, "lga"))),
                    AttributeValue(wire, "vlt",
                        AttributeValue(wire, "voltage",
                            AttributeValue(wire, "lvl"))),
                    AttributeValue(wire, "cur",
                        AttributeValue(wire, "current",
                            AttributeValue(wire, "lcr"))),
                    AttributeValue(wire, "pwr",
                        AttributeValue(wire, "power",
                            AttributeValue(wire, "lpw"))),
                    AttributeValue(wire, "mrk",
                        AttributeValue(wire, "lmk")));
            }
        }

        private static IEnumerable<string> SplitArticleValues(string value)
        {
            return value
                .Split(new[] { ';', ',', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item));
        }

        private static IEnumerable<TemplateTreeItem> FlattenTemplateItems(IEnumerable<IItemProjectTree> items)
        {
            foreach (var item in items.OfType<TemplateTreeItem>())
            {
                yield return item;

                foreach (var child in FlattenTemplateItems(item.Children))
                {
                    yield return child;
                }
            }
        }

        private static double ParsePositiveDouble(string value, double fallback)
        {
            var parsed = ParseDouble(value);
            return parsed.HasValue && parsed.Value >= 0.0
                ? parsed.Value
                : fallback;
        }

        private sealed record MatchedComponent(XElement Component, TemplateTreeItem Template, string Article);

        private sealed record ComponentDatapoint(string Key, string Label, string Input, string Signal, string Unit, string Scale, string Uid, string Mark);

        private sealed record ComponentWire(string Key, string Counter, string Function, string Type, string Gauge, string Voltage, string Current, string Power, string Mark);

        private sealed class LoopReferenceContext
        {
            private readonly Dictionary<MatchedComponent, List<XElement>> preparedElements = new();

            public void AddPreparedElements(MatchedComponent matchedComponent, List<XElement> elements)
            {
                preparedElements.Add(matchedComponent, elements);
            }

            public IReadOnlyList<XElement> GetPreparedElements(MatchedComponent matchedComponent)
            {
                return preparedElements[matchedComponent];
            }

        }

        private sealed class NaturalStringComparer : IComparer<string>
        {
            public int Compare(string? left, string? right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (left == null)
                {
                    return -1;
                }

                if (right == null)
                {
                    return 1;
                }

                var leftIndex = 0;
                var rightIndex = 0;
                while (leftIndex < left.Length && rightIndex < right.Length)
                {
                    if (char.IsDigit(left[leftIndex]) && char.IsDigit(right[rightIndex]))
                    {
                        var leftStart = leftIndex;
                        var rightStart = rightIndex;
                        while (leftIndex < left.Length && char.IsDigit(left[leftIndex])) leftIndex++;
                        while (rightIndex < right.Length && char.IsDigit(right[rightIndex])) rightIndex++;

                        var leftNumber = left.AsSpan(leftStart, leftIndex - leftStart).TrimStart('0');
                        var rightNumber = right.AsSpan(rightStart, rightIndex - rightStart).TrimStart('0');
                        if (leftNumber.Length != rightNumber.Length)
                        {
                            return leftNumber.Length.CompareTo(rightNumber.Length);
                        }

                        var numberComparison = leftNumber.CompareTo(rightNumber, StringComparison.Ordinal);
                        if (numberComparison != 0)
                        {
                            return numberComparison;
                        }

                        continue;
                    }

                    var characterComparison = char.ToUpperInvariant(left[leftIndex]).CompareTo(char.ToUpperInvariant(right[rightIndex]));
                    if (characterComparison != 0)
                    {
                        return characterComparison;
                    }

                    leftIndex++;
                    rightIndex++;
                }

                return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
            }
        }

        private void AddTemplateGroup_Click(object sender, RoutedEventArgs e)
        {
            var group = new TemplateTreeItem
            {
                Kind = TemplateTreeItemKind.Group,
                Name = $"Gruppe {TemplateItems.OfType<TemplateTreeItem>().Count(item => item.Kind == TemplateTreeItemKind.Group) + 1}"
            };

            TemplateItems.Add(group);
            SelectedTemplateItem = group;
            RefreshTemplateStatus();
        }

        private void AddTemplate_Click(object sender, RoutedEventArgs e)
        {
            var group = SelectedTemplateItem?.Kind == TemplateTreeItemKind.Group
                ? SelectedTemplateItem
                : FindTemplateParent(TemplateItems, SelectedTemplateItem) ?? TemplateItems.OfType<TemplateTreeItem>().FirstOrDefault(item => item.Kind == TemplateTreeItemKind.Group);

            if (group == null)
            {
                group = new TemplateTreeItem
                {
                    Kind = TemplateTreeItemKind.Group,
                    Name = "Gruppe 1"
                };
                TemplateItems.Add(group);
            }

            group.IsExpanded = true;
            var template = new TemplateTreeItem
            {
                Kind = TemplateTreeItemKind.Template,
                Name = $"Vorlage {group.Children.OfType<TemplateTreeItem>().Count(item => item.Kind == TemplateTreeItemKind.Template) + 1}"
            };

            group.Children.Add(template);
            SelectedTemplateItem = template;
            RefreshTemplateStatus();
        }

        private void AddPageTemplate_Click(object sender, RoutedEventArgs e)
        {
            var group = FindOrCreatePageTemplateGroup();

            group.IsExpanded = true;
            var template = new TemplateTreeItem
            {
                Kind = TemplateTreeItemKind.PageTemplate,
                Name = $"Seitentemplate {group.Children.OfType<TemplateTreeItem>().Count(item => item.Kind == TemplateTreeItemKind.PageTemplate) + 1}"
            };

            group.Children.Add(template);
            SelectedTemplateItem = template;
            RefreshTemplateStatus();
        }

        private TemplateTreeItem FindOrCreatePageTemplateGroup()
        {
            var group = TemplateItems
                .OfType<TemplateTreeItem>()
                .FirstOrDefault(item => item.Kind == TemplateTreeItemKind.Group && string.Equals(item.Name, "Seitentemplates", StringComparison.OrdinalIgnoreCase));

            if (group != null)
            {
                return group;
            }

            group = new TemplateTreeItem
            {
                Kind = TemplateTreeItemKind.Group,
                Name = "Seitentemplates"
            };
            TemplateItems.Add(group);
            return group;
        }

        private void RemoveTemplateItem_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTemplateItem == null)
            {
                TemplateStatus.Text = "Bitte Gruppe oder Vorlage auswählen";
                return;
            }

            var parent = FindTemplateParent(TemplateItems, SelectedTemplateItem);
            var collection = parent?.Children ?? TemplateItems;
            collection.Remove(SelectedTemplateItem);
            SelectedTemplateItem = null;
            RefreshTemplateStatus();
        }

        private void LoadTemplateTree_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Vorlagen laden",
                Filter = "Vorlagen XML (*.xml)|*.xml|Alle Dateien (*.*)|*.*"
            };

            if (!string.IsNullOrWhiteSpace(templateTreeFilePath))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(templateTreeFilePath);
                dialog.FileName = Path.GetFileName(templateTreeFilePath);
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                LoadTemplateTree(dialog.FileName);
            }
            catch (Exception exception)
            {
                TemplateStatus.Text = $"Vorlagen konnten nicht geladen werden: {exception.Message}";
            }
        }

        private void SaveTemplateTree_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Vorlagen speichern",
                Filter = "Vorlagen XML (*.xml)|*.xml|Alle Dateien (*.*)|*.*",
                FileName = string.IsNullOrWhiteSpace(templateTreeFilePath) ? "vorlagen.xml" : Path.GetFileName(templateTreeFilePath)
            };

            if (!string.IsNullOrWhiteSpace(templateTreeFilePath))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(templateTreeFilePath);
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                SaveTemplateTree(dialog.FileName);
            }
            catch (Exception exception)
            {
                TemplateStatus.Text = $"Vorlagen konnten nicht gespeichert werden: {exception.Message}";
            }
        }

        private void LoadTemplateTree(string filePath)
        {
            var document = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
            TemplateItems.Clear();

            var root = document.Root;
            if (root != null)
            {
                foreach (var element in root.Elements())
                {
                    var item = ReadTemplateTreeItem(element);
                    if (item != null)
                    {
                        TemplateItems.Add(item);
                    }
                }
            }

            templateTreeFilePath = filePath;
            SelectedTemplateItem = null;
            RefreshTemplateStatus(Path.GetFileName(filePath));
        }

        private void SaveTemplateTree(string filePath)
        {
            var document = new XDocument(
                new XElement("templates", TemplateItems.OfType<TemplateTreeItem>().Select(item => item.Xml())));

            document.Save(filePath);
            templateTreeFilePath = filePath;
            RefreshTemplateStatus(Path.GetFileName(filePath));
        }

        private static TemplateTreeItem? ReadTemplateTreeItem(XElement element)
        {
            var kind = element.Name.LocalName switch
            {
                "group" => TemplateTreeItemKind.Group,
                "template" => TemplateTreeItemKind.Template,
                "page-template" => TemplateTreeItemKind.PageTemplate,
                _ => (TemplateTreeItemKind?)null
            };

            if (kind == null)
            {
                return null;
            }

            var item = new TemplateTreeItem
            {
                Kind = kind.Value,
                Name = AttributeValue(element, "name"),
                XmlTemplate = AttributeValue(element, "xml-template"),
                Article = AttributeValue(element, "artikel"),
                SheetName = AttributeValue(element, "normblatt"),
                PageTemplateName = AttributeValue(element, "seitentemplate"),
                ColumnNeed = AttributeValue(element, "spalten-bedarf"),
                ColumnSpacing = AttributeValue(element, "spalten-abstand"),
                Priority = int.TryParse(AttributeValue(element, "prioritaet"), out var priority) ? priority : 100,
                SinglePage = ReadBooleanAttribute(element, "single-page"),
                IsExpanded = true
            };

            foreach (var childElement in element.Elements())
            {
                var child = ReadTemplateTreeItem(childElement);
                if (child != null)
                {
                    item.Children.Add(child);
                }
            }

            return item;
        }

        private static bool ReadBooleanAttribute(XElement element, string name)
        {
            var value = AttributeValue(element, name);
            return bool.TryParse(value, out var result)
                ? result
                : value == "1";
        }

        private void TemplateTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            SelectedTemplateItem = e.NewValue as TemplateTreeItem;
        }

        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is not TemplateTreeItem sourceItem)
            {
                dropInfo.Effects = DragDropEffects.None;
                return;
            }

            var targetItem = dropInfo.TargetItem as TemplateTreeItem;
            if (!CanDropTemplateItem(sourceItem, targetItem))
            {
                dropInfo.Effects = DragDropEffects.None;
                return;
            }

            dropInfo.Effects = DragDropEffects.Move;
            dropInfo.DropTargetAdorner = targetItem?.Kind == TemplateTreeItemKind.Group && sourceItem.Kind == TemplateTreeItemKind.Template
                ? DropTargetAdorners.Highlight
                : DropTargetAdorners.Insert;
        }

        void IDropTarget.Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is not TemplateTreeItem sourceItem)
            {
                return;
            }

            var targetItem = dropInfo.TargetItem as TemplateTreeItem;
            if (!CanDropTemplateItem(sourceItem, targetItem))
            {
                return;
            }

            var sourceParent = FindTemplateParent(TemplateItems, sourceItem);
            var sourceCollection = sourceParent?.Children ?? TemplateItems;
            var targetCollection = ResolveTemplateDropTargetCollection(sourceItem, targetItem);
            if (targetCollection == null)
            {
                return;
            }

            var oldIndex = sourceCollection.IndexOf(sourceItem);
            var insertIndex = ResolveTemplateInsertIndex(dropInfo, sourceItem, targetItem, targetCollection);
            sourceCollection.Remove(sourceItem);

            if (ReferenceEquals(sourceCollection, targetCollection) && oldIndex >= 0 && oldIndex < insertIndex)
            {
                insertIndex--;
            }

            insertIndex = Math.Clamp(insertIndex, 0, targetCollection.Count);
            targetCollection.Insert(insertIndex, sourceItem);
            SelectedTemplateItem = sourceItem;
            RefreshTemplateStatus();
        }

        private static bool CanDropTemplateItem(TemplateTreeItem sourceItem, TemplateTreeItem? targetItem)
        {
            if (ReferenceEquals(sourceItem, targetItem))
            {
                return false;
            }

            if (sourceItem.Kind == TemplateTreeItemKind.Group)
            {
                return targetItem == null || targetItem.Kind == TemplateTreeItemKind.Group;
            }

            return targetItem?.Kind is TemplateTreeItemKind.Group or TemplateTreeItemKind.Template or TemplateTreeItemKind.PageTemplate;
        }

        private ObservableCollection<IItemProjectTree>? ResolveTemplateDropTargetCollection(TemplateTreeItem sourceItem, TemplateTreeItem? targetItem)
        {
            if (sourceItem.Kind == TemplateTreeItemKind.Group)
            {
                return TemplateItems;
            }

            if (targetItem?.Kind == TemplateTreeItemKind.Group)
            {
                return targetItem.Children;
            }

            var targetParent = FindTemplateParent(TemplateItems, targetItem);
            return targetParent?.Children;
        }

        private static int ResolveTemplateInsertIndex(IDropInfo dropInfo, TemplateTreeItem sourceItem, TemplateTreeItem? targetItem, ObservableCollection<IItemProjectTree> targetCollection)
        {
            if (sourceItem.Kind is TemplateTreeItemKind.Template or TemplateTreeItemKind.PageTemplate && targetItem?.Kind == TemplateTreeItemKind.Group)
            {
                return targetCollection.Count;
            }

            var insertIndex = dropInfo.UnfilteredInsertIndex >= 0 ? dropInfo.UnfilteredInsertIndex : dropInfo.InsertIndex;
            return insertIndex >= 0 ? insertIndex : targetCollection.Count;
        }

        private static TemplateTreeItem? FindTemplateParent(ObservableCollection<IItemProjectTree> items, TemplateTreeItem? target)
        {
            if (target == null)
            {
                return null;
            }

            foreach (var item in items.OfType<TemplateTreeItem>())
            {
                if (item.Children.Contains(target))
                {
                    return item;
                }

                var nestedParent = FindTemplateParent(item.Children, target);
                if (nestedParent != null)
                {
                    return nestedParent;
                }
            }

            return null;
        }

        private void RefreshTemplateStatus(string? sourceName = null)
        {
            var groupCount = TemplateItems.OfType<TemplateTreeItem>().Count(item => item.Kind == TemplateTreeItemKind.Group);
            var templateCount = FlattenTemplateItems(TemplateItems).Count(item => item.Kind == TemplateTreeItemKind.Template);
            var pageTemplateCount = FlattenTemplateItems(TemplateItems).Count(item => item.Kind == TemplateTreeItemKind.PageTemplate);
            TemplateStatus.Text = string.IsNullOrWhiteSpace(sourceName)
                ? $"{groupCount} Gruppe(n), {templateCount} Vorlage(n), {pageTemplateCount} Seitentemplate(s)"
                : $"{groupCount} Gruppe(n), {templateCount} Vorlage(n), {pageTemplateCount} Seitentemplate(s): {sourceName}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
