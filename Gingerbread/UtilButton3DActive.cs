#region Namespaces
using System;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
#endregion

namespace Gingerbread
{
    public class UtilButton3DActive : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication uiapp, CategorySet selectedCategories)
        {
            try
            {
                Document doc = uiapp.ActiveUIDocument.Document;
                View view = doc.ActiveView;
                if (view is View3D)
                {
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}