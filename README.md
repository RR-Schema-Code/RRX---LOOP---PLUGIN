# RRX LOOP DemoTool

WPF-Anwendung zum Anzeigen und Bearbeiten einer RRX-XML-Projektstruktur und ihrer Vorlagen.

## Projektstruktur

- `RRX.Xml/`: WPF-Anwendung und XML-Projektbaum
- `RRX.DragDrop/`: Drag-and-drop-Komponente für die Oberfläche
- `RRX.Xml/RRX.Xml.slnx`: Projektmappe für beide Projekte

Die lokal gespeicherte Vorlagensammlung `vorlagen.xml`, Build-Ausgaben und benutzerspezifische Einstellungen gehören nicht zum Repository. Vorlagen können in der Anwendung geladen und gespeichert werden.

## Bauen

Voraussetzung ist das .NET 8 SDK unter Windows.

```powershell
dotnet build .\RRX.Xml\RRX.Xml.csproj -c Release
```

## Lizenzhinweis

`RRX.DragDrop` ist eine angepasste Fassung von [GongSolutions.WPF.DragDrop](https://github.com/punker76/gong-wpf-dragdrop). Der [BSD-3-Clause-Lizenztext](RRX.DragDrop/LICENSE) der ursprünglichen Rechteinhaber gilt für diese Komponente und muss bei der Weitergabe ihres Quellcodes oder ihrer DLL erhalten bleiben. Für den übrigen Anwendungscode ist derzeit keine eigene Lizenz festgelegt.
