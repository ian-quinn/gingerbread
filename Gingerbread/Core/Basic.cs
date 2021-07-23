#region Namespaces
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Autodesk.Revit.DB;
#endregion

namespace Gingerbread.Core
{
    public static class Basic
    {
        public static CurveLoop SimplifyCurveLoop(CurveLoop crvLoop)
        {
            if (crvLoop.IsOpen())
            {
                return crvLoop;
            }

            CurveLoop boundary = new CurveLoop();
            List<XYZ> vertices = new List<XYZ>() { };
            List<XYZ> reducedVertices = new List<XYZ>() { };

            foreach (Curve crv in crvLoop)
            {
                vertices.Add(crv.GetEndPoint(0));
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                Debug.Print(i.ToString());
                XYZ thisDirection, prevDirection;
                if (i == 0) { prevDirection = vertices[i] - vertices.Last(); }
                else { prevDirection = vertices[i] - vertices[i - 1]; }
                if (i == vertices.Count - 1) { thisDirection = vertices[0] - vertices[i]; }
                else { thisDirection = vertices[i + 1] - vertices[i]; }

                if (!thisDirection.Normalize().IsAlmostEqualTo(prevDirection.Normalize()))
                {
                    reducedVertices.Add(vertices[i]);
                }
            }

            Debug.Print("the curveloop has vertices: " + vertices.Count.ToString() + " and reducted to " + reducedVertices.Count.ToString());

            reducedVertices.Add(reducedVertices[0]);
            for (int i = 0; i < reducedVertices.Count - 1; i++)
            {
                boundary.Append(Line.CreateBound(reducedVertices[i], reducedVertices[i + 1]) as Curve);
            }
            return boundary;
        }

    }
}
