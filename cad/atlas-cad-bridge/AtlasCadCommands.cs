using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace Atlas.Cad.Bridge
{
    // POC ONLY: replace DevelopmentGate with signed, device-bound licensing before sale.
    public sealed class AtlasCadCommands
    {
        [CommandMethod("ATLAS_MLINE_BRIDGE")]
        public void DrawMline()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            if (!DevelopmentGate.IsAllowed()) { ed.WriteMessage("\n[ATLAS] Licenca nao configurada (modo demonstracao desabilitado)."); return; }
            var stylePrompt = ed.GetString(new PromptStringOptions("\nNome do estilo MLINE existente: ") { AllowSpaces = true });
            if (stylePrompt.Status != PromptStatus.OK) return;
            var points = new System.Collections.Generic.List<Point3d>();
            var first = ed.GetPoint("\nPrimeiro ponto: ");
            if (first.Status != PromptStatus.OK) return;
            points.Add(first.Value);
            while (true)
            {
                var opts = new PromptPointOptions("\nProximo ponto ou Enter para concluir: ") { AllowNone = true, UseBasePoint = true, BasePoint = points[points.Count - 1] };
                var next = ed.GetPoint(opts);
                if (next.Status == PromptStatus.None) break;
                if (next.Status != PromptStatus.OK) return;
                points.Add(next.Value);
            }
            if (points.Count < 2) { ed.WriteMessage("\n[ATLAS] Minimo de dois pontos."); return; }
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var named = (DBDictionary)tr.GetObject(doc.Database.NamedObjectsDictionaryId, OpenMode.ForRead);
                if (!named.Contains("ACAD_MLINESTYLE")) { ed.WriteMessage("\n[ATLAS] Dicionario de estilos ausente."); return; }
                var styles = (DBDictionary)tr.GetObject(named.GetAt("ACAD_MLINESTYLE"), OpenMode.ForRead);
                if (!styles.Contains(stylePrompt.StringResult)) { ed.WriteMessage("\n[ATLAS] Estilo nao encontrado; carregue-o antes de executar."); return; }
                var currentSpace = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);
                var mline = new Mline { Style = styles.GetAt(stylePrompt.StringResult), Normal = Vector3d.ZAxis, Scale = 1.0 };
                foreach (var p in points) mline.AppendSegment(p);
                currentSpace.AppendEntity(mline);
                tr.AddNewlyCreatedDBObject(mline, true);
                tr.Commit();
            }
            ed.WriteMessage("\n[ATLAS] MLINE criada. Entidade original nao foi alterada.");
        }
    }
    internal static class DevelopmentGate
    {
        // Intentionally fail closed. Never ship a demo bypass in production.
        public static bool IsAllowed() => false;
    }
}
