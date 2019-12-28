using System.Text;
using System.IO;
using System.Net;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KeynotesRTC
{
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            KeynoteTable keynoteTable = KeynoteTable.GetKeynoteTable(doc);
            string keynotesPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(keynoteTable.GetExternalFileReference().GetAbsolutePath());
            string keynotesDir = keynotesPath.Substring(0, keynotesPath.LastIndexOf('\\') + 1);
            string keynotesFile = keynotesPath.Substring(keynotesPath.LastIndexOf('\\') + 1);

            string lockFile = $"{keynotesDir}\\.{keynotesFile}.lock";
            if (File.Exists(lockFile))
            {
                string portal = File.ReadAllText(lockFile, Encoding.UTF8).Trim();
                WebRequest.Create(portal);
            }
            else
            {
                WebRequest.Create($"atom://teletype-revit-linker/new?file={keynotesPath}");
            }

            return Result.Succeeded;
        }
    }
}