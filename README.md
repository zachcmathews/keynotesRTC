# KeynotesRTC
Enables real-time collaboration on Revit keynotes using the Atom text editor and Teletype for Atom.

To install the add-in, download the latest zip file under [releases](https://github.com/zachcmathews/keynotesRTC/releases).
Extract the contents to your `C:\ProgramData\Autodesk\Revit\Addins\2019` folder. 
See the [teletype-revit-linker repository](https://github.com/zachcmathews/teletype-revit-linker) for subsequent 
installation instructions.

## High-Level Implementation Details
The add-in determines which keynote files are currently being edited on a shared network drive using dummy files with the 
.lock file extension. These files contain an atom teletype URI which is used to connect subsequent editors to the collaborative 
editing session. This collaborative session is created and destroyed automatically by the teletype-revit-linker package along with the dummy .lock files.
