using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using Atlas.Cad.Core.Bridge;
using Atlas.Cad.Core.Styles;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using AcRuntimeException = Autodesk.AutoCAD.Runtime.Exception;
using AcColor = Autodesk.AutoCAD.Colors.Color;
using AcColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;

[assembly: CommandClass(typeof(Atlas.AutoCAD.Adapter.AtlasBridgeCommands))]

namespace Atlas.AutoCAD.Adapter;

public sealed class AtlasBridgeCommands
{
    private const string AtlasRegAppName = "ATLAS_CAD";
    private const string SourceTagPrefix = "source:";
    private readonly SecureBridgeFileStore _store = new SecureBridgeFileStore();
    private readonly SecureCatalogStore _catalogStore = new SecureCatalogStore();

    [LispFunction("ATLAS_NEW_REQUEST_ID")]
    public ResultBuffer NewRequestId(ResultBuffer arguments) =>
        LispText(BridgeProtocol.NewRequestId());

    [LispFunction("ATLAS_BRIDGE_ROOT")]
    public ResultBuffer BridgeRoot(ResultBuffer arguments) =>
        LispText(_store.EnsureRoot());

    [LispFunction("ATLAS_CATALOG_STYLE_NAMES")]
    public ResultBuffer CatalogStyleNames(ResultBuffer arguments)
    {
        try
        {
            var styles = _catalogStore.Read();
            var values = new List<TypedValue>(styles.Count);
            foreach (var style in styles)
                values.Add(new TypedValue((int)LispDataType.Text, style.Name));
            return new ResultBuffer(values.ToArray());
        }
        catch (BridgeCommandException exception)
        {
            AcApplication.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
                $"\n[ATLAS] Catalog unavailable ({exception.Code}).");
            return new ResultBuffer(new TypedValue((int)LispDataType.Nil));
        }
    }

    [CommandMethod("ATLAS_INSTALL_CATALOG", CommandFlags.Modal)]
    public void InstallCatalog()
    {
        var editor = AcApplication.DocumentManager.MdiActiveDocument.Editor;
        var assemblyDirectory = Path.GetDirectoryName(typeof(AtlasBridgeCommands).Assembly.Location) ?? string.Empty;
        var sourcePath = Path.Combine(assemblyDirectory, "steel.resolved.atlascatalog.json");
        if (!File.Exists(sourcePath))
        {
            var prompt = editor.GetString(new PromptStringOptions(
                "\nFull path to the Atlas .atlascatalog.json file: ")
            {
                AllowSpaces = true
            });
            if (prompt.Status != PromptStatus.OK)
                return;
            sourcePath = (prompt.StringResult ?? string.Empty).Trim().Trim('"');
        }

        try
        {
            var count = _catalogStore.Install(sourcePath);
            editor.WriteMessage($"\n[ATLAS] Catalog installed and validated: {count} styles.");
        }
        catch (BridgeCommandException exception)
        {
            editor.WriteMessage($"\n[ATLAS] Catalog installation failed ({exception.Code}).");
        }
        catch (System.Exception)
        {
            editor.WriteMessage("\n[ATLAS] Catalog installation failed (unexpected_error).");
        }
    }

    [CommandMethod("ATLAS_DRAW_MLINE", CommandFlags.Modal)]
    public void DrawMline()
    {
        var document = AcApplication.DocumentManager.MdiActiveDocument;
        var editor = document.Editor;
        var prompt = editor.GetString(new PromptStringOptions("\nATLAS request ID: ")
        {
            AllowSpaces = false
        });

        if (prompt.Status != PromptStatus.OK)
            return;

        var requestId = (prompt.StringResult ?? string.Empty).Trim().ToLowerInvariant();
        if (!BridgeProtocol.IsValidRequestId(requestId))
        {
            editor.WriteMessage("\n[ATLAS] Invalid request ID.");
            return;
        }

        DrawMlineResponse response;
        try
        {
            var request = _store.ReadRequest(requestId);
            response = Execute(document, requestId, request);
        }
        catch (BridgeCommandException exception)
        {
            response = DrawMlineResponse.Fail(requestId, exception.Code, exception.Message);
        }
        catch (AcRuntimeException exception)
        {
            response = DrawMlineResponse.Fail(requestId, "autocad_error", exception.ErrorStatus.ToString());
        }
        catch (System.Exception)
        {
            response = DrawMlineResponse.Fail(requestId, "unexpected_error", "Unexpected bridge failure.");
        }
        finally
        {
            try
            {
                _store.DeleteRequest(requestId);
            }
            catch (System.Exception)
            {
                editor.WriteMessage("\n[ATLAS] Warning: request cleanup failed.");
            }
        }

        try
        {
            _store.WriteResponse(requestId, response);
        }
        catch (System.Exception)
        {
            editor.WriteMessage("\n[ATLAS] Response could not be persisted; source geometry was not modified.");
            return;
        }

        editor.WriteMessage(response.Success
            ? $"\n[ATLAS] MLINE created: {response.NewEntityHandle}."
            : $"\n[ATLAS] Failed ({response.ErrorCode}). Source geometry was not modified.");
    }

    [CommandMethod("ATLAS_ENSURE_STYLE", CommandFlags.Modal)]
    public void EnsureStyle()
    {
        var document = AcApplication.DocumentManager.MdiActiveDocument;
        var editor = document.Editor;
        var prompt = editor.GetString(new PromptStringOptions("\nATLAS style request ID: ")
        {
            AllowSpaces = false
        });
        if (prompt.Status != PromptStatus.OK)
            return;

        var requestId = (prompt.StringResult ?? string.Empty).Trim().ToLowerInvariant();
        if (!BridgeProtocol.IsValidRequestId(requestId))
        {
            editor.WriteMessage("\n[ATLAS] Invalid request ID.");
            return;
        }

        EnsureStyleResponse response;
        try
        {
            var request = _store.ReadEnsureStyleRequest(requestId);
            response = ExecuteEnsureStyle(document, requestId, request);
        }
        catch (BridgeCommandException exception)
        {
            response = EnsureStyleResponse.Fail(requestId, exception.Code, exception.Message);
        }
        catch (AcRuntimeException exception)
        {
            response = EnsureStyleResponse.Fail(requestId, "autocad_error", exception.ErrorStatus.ToString());
        }
        catch (System.Exception)
        {
            response = EnsureStyleResponse.Fail(requestId, "unexpected_error", "Unexpected style bridge failure.");
        }
        finally
        {
            try
            {
                _store.DeleteRequest(requestId);
            }
            catch (System.Exception)
            {
                editor.WriteMessage("\n[ATLAS] Warning: style request cleanup failed.");
            }
        }

        try
        {
            _store.WriteResponse(requestId, response);
        }
        catch (System.Exception)
        {
            editor.WriteMessage("\n[ATLAS] Style response could not be persisted.");
            return;
        }

        editor.WriteMessage(response.Success
            ? $"\n[ATLAS] MLINE style {response.Status}: {response.StyleName}."
            : $"\n[ATLAS] Style failed ({response.ErrorCode}). Existing styles were not overwritten.");
    }

    private EnsureStyleResponse ExecuteEnsureStyle(
        Document document,
        string commandRequestId,
        EnsureStyleRequest request)
    {
        if (!string.Equals(request.RequestId, commandRequestId, StringComparison.Ordinal))
            throw new BridgeCommandException("request_id_mismatch", "Payload request ID does not match the command request ID.");

        var validationErrors = EnsureStyleRequestValidator.Validate(request);
        if (validationErrors.Count > 0)
            throw new BridgeCommandException("validation_failed", string.Join(",", validationErrors));

        var database = document.Database;
        if (!DrawingIdentityMatches(request.ExpectedDrawingIdentity!, database.Filename, document.Name))
            throw new BridgeCommandException("drawing_mismatch", "Active drawing does not match the expected drawing identity.");

        var definition = ResolveStyleDefinition(request);
        using (var lookup = database.TransactionManager.StartTransaction())
        {
            var existingDictionary = (DBDictionary)lookup.GetObject(database.MLStyleDictionaryId, OpenMode.ForRead);
            if (existingDictionary.Contains(definition.Name))
            {
                var existing = (MlineStyle)lookup.GetObject(existingDictionary.GetAt(definition.Name), OpenMode.ForRead);
                if (!StyleMatches(existing, definition, lookup))
                    throw new BridgeCommandException("style_collision", "A different MLINE style already uses this name.");
                return EnsureStyleResponse.Ok(commandRequestId, definition.Name, "existing");
            }
        }

        EnsureLinetypes(database, definition);
        using var transaction = database.TransactionManager.StartTransaction();
        var dictionary = (DBDictionary)transaction.GetObject(database.MLStyleDictionaryId, OpenMode.ForWrite);
        using var style = CreateStyle(definition, database, transaction);
        dictionary.SetAt(definition.Name, style);
        transaction.AddNewlyCreatedDBObject(style, true);
        transaction.Commit();
        return EnsureStyleResponse.Ok(commandRequestId, definition.Name, "created");
    }

    private MlineStyleDefinition ResolveStyleDefinition(EnsureStyleRequest request)
    {
        if (request.Style != null)
            return request.Style;
        try
        {
            return MlineStyleCatalog.Find(_catalogStore.Read(), request.StyleName!);
        }
        catch (KeyNotFoundException)
        {
            throw new BridgeCommandException("catalog_style_not_found", "The requested style is not present in the installed catalog.");
        }
    }

    private static MlineStyle CreateStyle(
        MlineStyleDefinition definition,
        Database database,
        Transaction transaction)
    {
        var style = new MlineStyle
        {
            Name = definition.Name,
            Description = definition.Description,
            Filled = definition.Flags.HasFlag(MlineStyleFlags.Filled),
            ShowMiters = definition.Flags.HasFlag(MlineStyleFlags.ShowMiters),
            StartSquareCap = definition.Flags.HasFlag(MlineStyleFlags.StartSquareCap),
            StartInnerArcs = definition.Flags.HasFlag(MlineStyleFlags.StartInnerArcs),
            StartRoundCap = definition.Flags.HasFlag(MlineStyleFlags.StartRoundCap),
            EndSquareCap = definition.Flags.HasFlag(MlineStyleFlags.EndSquareCap),
            EndInnerArcs = definition.Flags.HasFlag(MlineStyleFlags.EndInnerArcs),
            EndRoundCap = definition.Flags.HasFlag(MlineStyleFlags.EndRoundCap),
            FillColor = ColorFromIndex(definition.FillColorIndex),
            StartAngle = DegreesToRadians(definition.StartAngleDegrees),
            EndAngle = DegreesToRadians(definition.EndAngleDegrees)
        };

        while (style.Elements.Count > 0)
            style.Elements.RemoveAt(style.Elements.Count - 1);

        foreach (var element in definition.Elements)
        {
            var linetypeId = ResolveLinetype(database, transaction, element.LinetypeName);
            style.Elements.Add(new MlineStyleElement(
                element.Offset,
                ColorFromIndex(element.ColorIndex),
                linetypeId), false);
        }
        return style;
    }

    private static bool StyleMatches(
        MlineStyle existing,
        MlineStyleDefinition definition,
        Transaction transaction)
    {
        var expectedFlags = definition.Flags;
        if (!string.Equals(existing.Name, definition.Name, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(existing.Description ?? string.Empty, definition.Description ?? string.Empty, StringComparison.Ordinal) ||
            existing.Filled != expectedFlags.HasFlag(MlineStyleFlags.Filled) ||
            existing.ShowMiters != expectedFlags.HasFlag(MlineStyleFlags.ShowMiters) ||
            existing.StartSquareCap != expectedFlags.HasFlag(MlineStyleFlags.StartSquareCap) ||
            existing.StartInnerArcs != expectedFlags.HasFlag(MlineStyleFlags.StartInnerArcs) ||
            existing.StartRoundCap != expectedFlags.HasFlag(MlineStyleFlags.StartRoundCap) ||
            existing.EndSquareCap != expectedFlags.HasFlag(MlineStyleFlags.EndSquareCap) ||
            existing.EndInnerArcs != expectedFlags.HasFlag(MlineStyleFlags.EndInnerArcs) ||
            existing.EndRoundCap != expectedFlags.HasFlag(MlineStyleFlags.EndRoundCap) ||
            existing.FillColor.ColorIndex != definition.FillColorIndex ||
            Math.Abs(existing.StartAngle - DegreesToRadians(definition.StartAngleDegrees)) > 1e-9 ||
            Math.Abs(existing.EndAngle - DegreesToRadians(definition.EndAngleDegrees)) > 1e-9 ||
            existing.Elements.Count != definition.Elements.Count)
            return false;

        for (var index = 0; index < definition.Elements.Count; index++)
        {
            var actual = existing.Elements[index];
            var expected = definition.Elements[index];
            var linetype = (LinetypeTableRecord)transaction.GetObject(actual.LinetypeId, OpenMode.ForRead);
            if (Math.Abs(actual.Offset - expected.Offset) > 1e-9 ||
                actual.Color.ColorIndex != expected.ColorIndex ||
                !string.Equals(linetype.Name, expected.LinetypeName, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static void EnsureLinetypes(Database database, MlineStyleDefinition definition)
    {
        var names = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in definition.Elements)
            names.Add(element.LinetypeName);

        foreach (var name in names)
        {
            if (LinetypeExists(database, name))
                continue;
            try
            {
                database.LoadLineTypeFile(name, "acad.lin");
            }
            catch (AcRuntimeException)
            {
                try
                {
                    database.LoadLineTypeFile(name, "acadiso.lin");
                }
                catch (AcRuntimeException)
                {
                    throw new BridgeCommandException("linetype_not_found", "Required linetype could not be loaded: " + name);
                }
            }
            if (!LinetypeExists(database, name))
                throw new BridgeCommandException("linetype_not_found", "Required linetype could not be loaded: " + name);
        }
    }

    private static bool LinetypeExists(Database database, string name)
    {
        using var transaction = database.TransactionManager.StartOpenCloseTransaction();
        var table = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
        return table.Has(name);
    }

    private static ObjectId ResolveLinetype(Database database, Transaction transaction, string name)
    {
        var table = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
        if (!table.Has(name))
            throw new BridgeCommandException("linetype_not_found", "Required linetype is not present: " + name);
        return table[name];
    }

    private static AcColor ColorFromIndex(int index)
    {
        var method = index == 0
            ? AcColorMethod.ByBlock
            : index == 256 ? AcColorMethod.ByLayer : AcColorMethod.ByAci;
        return AcColor.FromColorIndex(method, (short)index);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static DrawMlineResponse Execute(Document document, string commandRequestId, DrawMlineRequest request)
    {
        if (!string.Equals(request.RequestId, commandRequestId, StringComparison.Ordinal))
            throw new BridgeCommandException("request_id_mismatch", "Payload request ID does not match the command request ID.");

        var validationErrors = BridgeContractValidator.Validate(request);
        if (validationErrors.Count > 0)
            throw new BridgeCommandException("validation_failed", string.Join(",", validationErrors));

        var database = document.Database;
        if (!DrawingIdentityMatches(request.ExpectedDrawingIdentity!, database.Filename, document.Name))
            throw new BridgeCommandException("drawing_mismatch", "Active drawing does not match the expected drawing identity.");

        using var transaction = database.TransactionManager.StartTransaction();
        var styleId = ResolveStyle(database, transaction, request.StyleName!);
        var layerId = ResolveLayer(database, transaction, request.LayerId, request.LayerName);
        ValidateSourceEntity(database, transaction, request.SourceHandle);

        if (!string.IsNullOrWhiteSpace(request.SourceHandle))
        {
            var recoveredHandle = FindExistingMlineForSource(
                database,
                transaction,
                styleId,
                layerId,
                request.SourceHandle!);
            if (recoveredHandle != null)
                return DrawMlineResponse.Ok(commandRequestId, recoveredHandle);
        }

        var normal = request.NormalWcs!;
        using var mline = new Mline
        {
            Style = styleId,
            LayerId = layerId,
            Normal = new Vector3d(normal.X, normal.Y, normal.Z).GetNormal(),
            Justification = (MlineJustification)request.Justification,
            Scale = request.Scale
        };

        foreach (var point in request.PointsWcs!)
            mline.AppendSegment(new Point3d(point.X, point.Y, point.Z));
        mline.IsClosed = request.Closed;

        var currentSpace = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
        currentSpace.AppendEntity(mline);
        transaction.AddNewlyCreatedDBObject(mline, true);
        if (!string.IsNullOrWhiteSpace(request.SourceHandle))
        {
            EnsureAtlasRegApp(database, transaction);
            mline.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, AtlasRegAppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, SourceTagPrefix + request.SourceHandle!.ToUpperInvariant()));
        }
        var handle = mline.Handle.ToString();
        transaction.Commit();

        return DrawMlineResponse.Ok(commandRequestId, handle);
    }

    private static string? FindExistingMlineForSource(
        Database database,
        Transaction transaction,
        ObjectId styleId,
        ObjectId layerId,
        string sourceHandle)
    {
        var expectedTag = SourceTagPrefix + sourceHandle.ToUpperInvariant();
        var currentSpace = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
        foreach (ObjectId objectId in currentSpace)
        {
            if (!(transaction.GetObject(objectId, OpenMode.ForRead, false) is Mline candidate) ||
                candidate.Style != styleId || candidate.LayerId != layerId)
                continue;

            using var data = candidate.GetXDataForApplication(AtlasRegAppName);
            if (data == null)
                continue;
            foreach (var value in data)
                if (value.TypeCode == (int)DxfCode.ExtendedDataAsciiString &&
                    string.Equals(value.Value as string, expectedTag, StringComparison.OrdinalIgnoreCase))
                    return candidate.Handle.ToString();
        }
        return null;
    }

    private static void EnsureAtlasRegApp(Database database, Transaction transaction)
    {
        var table = (RegAppTable)transaction.GetObject(database.RegAppTableId, OpenMode.ForRead);
        if (table.Has(AtlasRegAppName))
            return;
        table.UpgradeOpen();
        using var record = new RegAppTableRecord { Name = AtlasRegAppName };
        table.Add(record);
        transaction.AddNewlyCreatedDBObject(record, true);
    }

    private static ObjectId ResolveStyle(Database database, Transaction transaction, string styleName)
    {
        var styles = (DBDictionary)transaction.GetObject(database.MLStyleDictionaryId, OpenMode.ForRead);
        if (!styles.Contains(styleName))
            throw new BridgeCommandException("style_not_found", "Synthetic MLINE style was not found in the drawing.");
        return styles.GetAt(styleName);
    }

    private static ObjectId ResolveLayer(
        Database database,
        Transaction transaction,
        string? layerHandle,
        string? layerName)
    {
        if (!string.IsNullOrWhiteSpace(layerHandle))
        {
            var objectId = ObjectIdFromHandle(database, layerHandle!, "layer_not_found");
            var record = transaction.GetObject(objectId, OpenMode.ForRead) as LayerTableRecord ??
                throw new BridgeCommandException("layer_not_found", "Layer handle does not identify a layer.");
            if (!string.IsNullOrWhiteSpace(layerName) &&
                !string.Equals(record.Name, layerName, StringComparison.OrdinalIgnoreCase))
                throw new BridgeCommandException("layer_mismatch", "Layer name and handle identify different layers.");
            return objectId;
        }

        var layers = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (string.IsNullOrWhiteSpace(layerName) || !layers.Has(layerName))
            throw new BridgeCommandException("layer_not_found", "Requested layer was not found.");
        return layers[layerName];
    }

    private static void ValidateSourceEntity(Database database, Transaction transaction, string? sourceHandle)
    {
        if (string.IsNullOrWhiteSpace(sourceHandle))
            return;

        var objectId = ObjectIdFromHandle(database, sourceHandle!, "source_not_found");
        if (!(transaction.GetObject(objectId, OpenMode.ForRead, false) is Entity))
            throw new BridgeCommandException("source_not_found", "Source handle does not identify an entity.");
    }

    private static ObjectId ObjectIdFromHandle(Database database, string value, string errorCode)
    {
        if (!long.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rawHandle))
            throw new BridgeCommandException(errorCode, "Object handle is invalid.");

        try
        {
            return database.GetObjectId(false, new Handle(rawHandle), 0);
        }
        catch (AcRuntimeException)
        {
            throw new BridgeCommandException(errorCode, "Object handle was not found in the active drawing.");
        }
    }

    private static bool DrawingIdentityMatches(string expected, string databaseFilename, string documentName)
    {
        var actual = string.IsNullOrWhiteSpace(databaseFilename) ? documentName : databaseFilename;
        return string.Equals(NormalizeDrawingIdentity(expected), NormalizeDrawingIdentity(actual), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDrawingIdentity(string value)
    {
        var trimmed = value.Trim();
        try
        {
            return Path.IsPathRooted(trimmed) ? Path.GetFullPath(trimmed).TrimEnd(Path.DirectorySeparatorChar) : trimmed;
        }
        catch (System.Exception)
        {
            return trimmed;
        }
    }

    private static ResultBuffer LispText(string value) =>
        new ResultBuffer(new TypedValue((int)LispDataType.Text, value));
}
