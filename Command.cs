using System.Text;
using System.IO;
using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KeynotesRTC
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            KeynoteTable keynoteTable = KeynoteTable.GetKeynoteTable(doc);
            string keynotesPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(keynoteTable.GetExternalFileReference().GetAbsolutePath());
            string lockFile = $"{keynotesPath}.lock";
            if (File.Exists(lockFile))
            {
                string uri = File.ReadAllText(lockFile, Encoding.UTF8).Trim();
                ProcessStartInfo info = new ProcessStartInfo();
                info.UseShellExecute = true;
                info.FileName = uri;
                Process.Start(info);
            }
            else
            {
                string uri = $"atom://teletype-revit-linker/new?file={keynotesPath}";
                ProcessStartInfo info = new ProcessStartInfo();
                info.UseShellExecute = true;
                info.FileName = uri;
                Process.Start(info);
            }

            return Result.Succeeded;
        }
    }
}