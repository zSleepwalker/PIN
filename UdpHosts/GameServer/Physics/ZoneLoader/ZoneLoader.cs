using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities;
using BepuUtilities.Memory;
using GameServer.Physics.TagfileLoader;
using Serilog;
using static GameServer.Physics.ZoneLoader.BepuData;
using BaseTagfileObject = GameServer.Physics.TagfileLoader.BaseTagfileObject;
using ENWFLayer = GameServer.Physics.ZoneLoader.ENWFData.ENWFLayer;
using HkpBoxShapeObject = GameServer.Physics.TagfileLoader.HkpBoxShapeObject;
using HkpCapsuleShapeObject = GameServer.Physics.TagfileLoader.HkpCapsuleShapeObject;
using HkpConvexTransformShapeObject = GameServer.Physics.TagfileLoader.HkpConvexTransformShapeObject;
using HkpConvexTranslateShapeObject = GameServer.Physics.TagfileLoader.HkpConvexTranslateShapeObject;
using HkpConvexVerticesShapeObject = GameServer.Physics.TagfileLoader.HkpConvexVerticesShapeObject;
using HkpCylinderShapeObject = GameServer.Physics.TagfileLoader.HkpCylinderShapeObject;
using HkpExtendedMeshShapeObject = GameServer.Physics.TagfileLoader.HkpExtendedMeshShapeObject;
using HkpListShapeObject = GameServer.Physics.TagfileLoader.HkpListShapeObject;
using HkpMoppBvTreeShapeObject = GameServer.Physics.TagfileLoader.HkpMoppBvTreeShapeObject;
using HkpSphereShapeObject = GameServer.Physics.TagfileLoader.HkpSphereShapeObject;
using HkpTransformShapeObject = GameServer.Physics.TagfileLoader.HkpTransformShapeObject;

namespace GameServer.Physics.ZoneLoader;

public class ZoneLoader
{
    private static readonly ILogger _logger = Log.ForContext<ZoneLoader>();

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true
    };

    public ZoneLoader(Simulation simulation, BufferPool pool, ThreadDispatcher dispatcher, TagfileLoader.TagfileLoader tagfileLoader)
    {
        Simulation = simulation;
        BufferPool = pool;
        ThreadDispatcher = dispatcher;
        TagfileLoader = tagfileLoader;

        _serializerOptions.Converters.Add(new TagfileObjectJsonConverter());
        _serializerOptions.Converters.Add(new Vector4Converter());
        _serializerOptions.Converters.Add(new Vector3Converter());
        _serializerOptions.Converters.Add(new StringBooleanConverter());

        PlaceholderBox = Simulation.Shapes.Add(new Box(0.5f * 2, 0.5f * 2, 0.5f * 2));
    }

    public Simulation Simulation { get; protected set; }
    public BufferPool BufferPool { get; private set; }
    public ThreadDispatcher ThreadDispatcher { get; private set; }
    public TagfileLoader.TagfileLoader TagfileLoader { get; private set; }
    public TypedIndex PlaceholderBox { get; private set; }

    public bool LoadCollision(string mapsPath, uint zoneId)
    {
        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();

        var zoneFilePath = Path.Combine(mapsPath, $"{zoneId}.pinzone.json");
        PinZone zoneData = LoadZoneJSON(zoneFilePath);
        if (zoneData == null)
        {
            _logger.Error("Failed to load {zoneFilePath}", zoneFilePath);
            return false;
        }

        _logger.Information("Loading {ChunkCount} chunks", zoneData.Chunks.Length);
        var counter = 0;
        foreach (var chunk in zoneData.Chunks)
        {
            var chunkFilePath = Path.Combine(mapsPath, "chunks", $"{chunk.Name}.pinchunk.json");
            var success = LoadChunkJSON(chunk.Origin, chunkFilePath);
            _logger.Information("({Counter}/{ChunkCount}) Chunk {ChunkName} {Status}", ++counter, zoneData.Chunks.Length, chunk.Name, success ? "Loaded" : "Failed");
        }

        stopWatch.Stop();
        TimeSpan ts = stopWatch.Elapsed;
        string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours,
            ts.Minutes,
            ts.Seconds,
            ts.Milliseconds / 10);

        _logger.Information("LoadCollision Finished in {ElapsedTime}", elapsedTime);
        return true;
    }

    private PinZone LoadZoneJSON(string path)
    {
        _logger.Information("LoadZoneJSON {Path}", path);
        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PinZone>(json, _serializerOptions);
        }
        catch (Exception e)
        {
            _logger.Error("LoadZoneJSON Failed: {Message} ({Type})", e.Message, e.GetType().Name);
            return null;
        }
    }

    private bool LoadChunkJSON(Vector3 origin, string path)
    {
        _logger.Debug("LoadChunkJSON {Path}", path);
        try
        {
            string json = File.ReadAllText(path);
            PinChunk chunk = JsonSerializer.Deserialize<PinChunk>(json, _serializerOptions);

            foreach (PinChunkSubChunk subChunk in chunk.SubChunks)
            {
                LoadChunkLayer(origin, subChunk.Cg);
                LoadChunkLayer(origin, subChunk.Cg2);
                LoadChunkLayer(origin, subChunk.Cg3);
            }

            return true;
        }
        catch (Exception e)
        {
            _logger.Error("LoadChunkJSON Failed: {Message} ({Type}) on {Path}\n{StackTrace}", e.Message, e.GetType().Name, path, e.StackTrace);
            return false;
        }
    }

    private void LoadChunkLayer(Vector3 origin, ENWFLayer layer)
    {
        if (layer == null)
        {
            return;
        }

        var storage = layer as ITagfileExternalStorage;
        var root = layer.GetTagfileObject("#0001");
        var statics = TagfileLoader.ProcessObject(root, ref storage);
        for (int i = 0; i < statics.Length; i++)
        {
            var stat = statics[i];
            stat.Pose.Position += origin;
            Simulation.Statics.Add(stat);
        }
    }

    private StaticDescription[] ProcessChunkObject(BaseTagfileObject obj, ref ENWFLayer layer)
    {
        switch (obj)
        {
            // Shapes
            case HkpBoxShapeObject box:
                return ProcessShape(box, ref layer);
            case HkpSphereShapeObject sphere:
                return ProcessShape(sphere, ref layer);
            case HkpCapsuleShapeObject capsule:
                return ProcessShape(capsule, ref layer);
            case HkpCylinderShapeObject cylinder:
                return ProcessShape(cylinder, ref layer);
            case HkpExtendedMeshShapeObject extendedMesh:
                return ProcessShape(extendedMesh, ref layer);
            case HkpConvexVerticesShapeObject convexVertices:
                return ProcessShape(convexVertices, ref layer);

            // Containers
            case HkpListShapeObject list:
                return ProcessContainer(list, ref layer);
            case HkpMoppBvTreeShapeObject moppBvTree:
                return ProcessContainer(moppBvTree, ref layer);

            // Modifiers
            case HkpConvexTranslateShapeObject convexTranslate:
                return ProcessModifier(convexTranslate, ref layer);
            case HkpTransformShapeObject transform:
                return ProcessModifier(transform, ref layer);
            case HkpConvexTransformShapeObject convexTransform:
                return ProcessModifier(convexTransform, ref layer);
        }

        // Serilog.Log.Information($"Failed to ProcessChunkObject with {obj}");
        throw new NotImplementedException($"ProcessChunkObject could not process an object {obj}");
    }

    private StaticDescription[] ProcessContainer(HkpListShapeObject obj, ref ENWFLayer layer)
    {
        List<StaticDescription> result = new();

        foreach (var childInfo in obj.ChildInfo)
        {
            var childObj = layer.GetTagfileObject(childInfo.Shape);
            try
            {
                var childStaticArr = ProcessChunkObject(childObj, ref layer);
                result.AddRange(childStaticArr);
            }
            catch (NotImplementedException)
            {
                _logger.Warning("Ignoring child {Child} of {Parent} because support is not implemented", childObj, obj);
            }
        }

        return result.ToArray();
    }

    private StaticDescription[] ProcessContainer(HkpMoppBvTreeShapeObject obj, ref ENWFLayer layer)
    {
        var childObj = layer.GetTagfileObject(obj.Child);
        return ProcessChunkObject(childObj, ref layer);
    }

    private StaticDescription[] ProcessModifier(HkpConvexTranslateShapeObject obj, ref ENWFLayer layer)
    {
        var childShapeObj = layer.GetTagfileObject(obj.ChildShape);
        var childShapeStaticArr = ProcessChunkObject(childShapeObj, ref layer);

        var pos = new Vector3(obj.Translation[0], obj.Translation[1], obj.Translation[2]);

        return childShapeStaticArr.Select((StaticDescription childShapeStatic) =>
        {
            childShapeStatic.Pose.Position = pos;
            return childShapeStatic;
        }).ToArray();
    }

    private StaticDescription[] ProcessModifier(HkpTransformShapeObject obj, ref ENWFLayer layer)
    {
        var childShapeObj = layer.GetTagfileObject(obj.ChildShape);
        var childShapeStaticArr = ProcessChunkObject(childShapeObj, ref layer);

        var rot = new Quaternion(obj.Rotation[0], obj.Rotation[1], obj.Rotation[2], obj.Rotation[3]);
        var pos = new Vector3(obj.Transform[3][0], obj.Transform[3][1], obj.Transform[3][2]);

        return childShapeStaticArr.Select((StaticDescription childShapeStatic) =>
        {
            if (childShapeStatic.Shape.Type == Mesh.Id)
            {
                /*
                Spent a day testing before landing on this fix, hopefully it lasts...
                The issue occurs when hkpExtendedMeshShape triangle subpart has translation, and then somewhere in the lineage there is a parent hkpTransformShape to position the whole shape in the world.
                Calling Recenter was the only thing that seemed to help, I suspect there may be some relation to these meshes having scaling as well.
                However, we still have other hkpExtendedMeshShapes with triangle subparts and translation that are not supposed to be repositioned, so I landed on calling it before we reposition it with the transform.
                */
                ref var mesh = ref Simulation.Shapes.GetShape<Mesh>(childShapeStatic.Shape.Index);
                mesh.Recenter(-childShapeStatic.Pose.Position);
            }

            childShapeStatic.Pose.Orientation = rot;
            childShapeStatic.Pose.Position = pos;
            return childShapeStatic;
        }).ToArray();
    }

    private StaticDescription[] ProcessModifier(HkpConvexTransformShapeObject obj, ref ENWFLayer layer)
    {
        var childShapeObj = layer.GetTagfileObject(obj.ChildShape);
        var childShapeStaticArr = ProcessChunkObject(childShapeObj, ref layer);

        var pos = new Vector3(obj.Transform[3][0], obj.Transform[3][1], obj.Transform[3][2]);
        var matrix = new Matrix4x4(
            obj.Transform[0][0],
            obj.Transform[0][1],
            obj.Transform[0][2],
            0,
            obj.Transform[1][0],
            obj.Transform[1][1],
            obj.Transform[1][2],
            0,
            obj.Transform[2][0],
            obj.Transform[2][1],
            obj.Transform[2][2],
            0,
            0,
            0,
            0,
            0);
        var rot = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(matrix));

        return childShapeStaticArr.Select((StaticDescription childShapeStatic) =>
        {
            childShapeStatic.Pose.Orientation = rot;
            childShapeStatic.Pose.Position = pos;
            return childShapeStatic;
        }).ToArray();
    }

    private StaticDescription[] ProcessShape(HkpBoxShapeObject obj, ref ENWFLayer layer)
    {
        var box = new Box(obj.HalfExtents[0] * 2, obj.HalfExtents[1] * 2, obj.HalfExtents[2] * 2);
        var stat = new StaticDescription(RigidPose.Identity, Simulation.Shapes.Add(box));
        return [stat];
    }

    private StaticDescription[] ProcessShape(HkpSphereShapeObject obj, ref ENWFLayer layer)
    {
        var sphere = new Sphere(obj.Radius);
        var stat = new StaticDescription(RigidPose.Identity, Simulation.Shapes.Add(sphere));
        return [stat];
    }

    private StaticDescription[] ProcessShape(HkpCapsuleShapeObject obj, ref ENWFLayer layer)
    {
        var top = new Vector3(obj.VertexA[0], obj.VertexA[1], obj.VertexA[2]);
        var bot = new Vector3(obj.VertexB[0], obj.VertexB[1], obj.VertexB[2]);
        var mid = Vector3.Multiply(Vector3.Add(top, bot), 0.5f);
        var len = Vector3.Distance(top, bot);
        var dir = Vector3.Normalize(Vector3.Subtract(bot, top));
        var up = new Vector3(0, 1, 0); // yes... idk why

        QuaternionEx.GetQuaternionBetweenNormalizedVectors(up, dir, out Quaternion rot);
        var rad = obj.Radius;
        var capsule = new Capsule(rad, len);

        var pose = RigidPose.Identity;
        pose.Orientation = rot;
        pose.Position = mid;

        var stat = new StaticDescription(pose, Simulation.Shapes.Add(capsule));

        // TEMP
        if (float.IsNaN(stat.Pose.Orientation.X))
        {
            _logger.Error("CAPSULE {Name} {Rot}", obj.Name, rot);
            _logger.Error("CAPSULE {Name} STAT {Orientation}", obj.Name, stat.Pose.Orientation);
            throw new Exception();
        }

        return [stat];
    }

    private StaticDescription[] ProcessShape(HkpCylinderShapeObject obj, ref ENWFLayer layer)
    {
        var top = new Vector3(obj.VertexA[0], obj.VertexA[1], obj.VertexA[2]);
        var bot = new Vector3(obj.VertexB[0], obj.VertexB[1], obj.VertexB[2]);
        var mid = Vector3.Multiply(Vector3.Add(top, bot), 0.5f);
        var len = Vector3.Distance(top, bot);
        var dir = Vector3.Normalize(Vector3.Subtract(bot, top));
        var up = new Vector3(0, 1, 0); // yes... idk why
        QuaternionEx.GetQuaternionBetweenNormalizedVectors(up, dir, out Quaternion rot);
        var rad = obj.CylRadius;
        var cylinder = new Cylinder(rad, len);

        var pose = RigidPose.Identity;
        pose.Orientation = rot;
        pose.Position = mid;

        var stat = new StaticDescription(pose, Simulation.Shapes.Add(cylinder));
        return [stat];
    }

    private StaticDescription[] ProcessShape(HkpExtendedMeshShapeObject obj, ref ENWFLayer layer)
    {
        List<StaticDescription> result = new();

        foreach (var tripart in obj.TrianglesSubparts)
        {
            ushort blockIndicator = (ushort)(tripart.UserData & 0xFFFF);
            ref var vertices = ref layer.VertBlocks[blockIndicator].Verts;
            ref var indices = ref layer.IndiceBlocks[blockIndicator].Indices;
            var triangles = new TriangleContent[indices.Length];
            for (uint indiceIdx = 0; indiceIdx < indices.Length; indiceIdx++)
            {
                ref var indice = ref indices[indiceIdx];
                ref var triangle = ref triangles[indiceIdx];
                triangle.A = vertices[indice[2]];
                triangle.B = vertices[indice[1]];
                triangle.C = vertices[indice[0]];
            }

            var meshContent = new MeshContent(triangles);

            var transform = tripart.Transform;
            var rot = Quaternion.Normalize(new Quaternion(transform[1][0], transform[1][1], transform[1][2], transform[1][3]));
            var scale = new Vector3(transform[2][0], transform[2][1], transform[2][2]);
            var pos = new Vector3(transform[0][0], transform[0][1], transform[0][2]);

            var mesh = LoadMeshContent(meshContent, BufferPool, scale, ThreadDispatcher);

            var pose = RigidPose.Identity;
            pose.Orientation = rot;
            pose.Position = pos;

            result.Add(new StaticDescription(pose, Simulation.Shapes.Add(mesh)));
        }

        foreach (var shapepart in obj.ShapesSubparts)
        {
            foreach (var childShape in shapepart.ChildShapes)
            {
                // TODO: Consider rotation and translation of subpart shape
                var childShapeObj = layer.GetTagfileObject(childShape);
                try
                {
                    var childShapeStaticArr = ProcessChunkObject(childShapeObj, ref layer);
                    result.AddRange(childShapeStaticArr);
                }
                catch (NotImplementedException)
                {
                    // Serilog.Log.Information($"Ignoring child {childShapeObj} of {shapepart} because support is not implemented");
                }
            }
        }

        return result.ToArray();
    }

    private StaticDescription[] ProcessShape(HkpConvexVerticesShapeObject obj, ref ENWFLayer layer)
    {
        try
        {
            Vector3[] vertices = UnrotateRotatedVertices(obj.RotatedVertices, obj.NumVertices);
            ConvexHullHelper.ComputeHull(vertices, BufferPool, out HullData hullData);
            if (IsHullFaceValid(vertices, hullData))
            {
                ConvexHullHelper.CreateShape(vertices, hullData, BufferPool, out Vector3 center, out ConvexHull convexHull);
                var pose = new RigidPose(center);
                var stat = new StaticDescription(pose, Simulation.Shapes.Add(convexHull));
                return [stat];
            }

            _logger.Error("Failed to process hkpConvexVerticesShape {Name}. IsHullFaceValid reports false.", obj.Name);
            return [new StaticDescription(RigidPose.Identity, PlaceholderBox)];
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to process hkpConvexVerticesShape {Name}. Exception: {Message} ({Type})\n{StackTrace}", obj.Name, ex.Message, ex.GetType().Name, ex.StackTrace);
            return [new StaticDescription(RigidPose.Identity, PlaceholderBox)];
        }
    }

    private static Vector3[] UnrotateRotatedVertices(Vector4[][] rotatedVertices, uint numVertices)
    {
        Vector3[] vertices = new Vector3[numVertices];
        var vert = 0;
        for (int i = 0; i < rotatedVertices.Length; i++)
        {
            vertices[vert++] = new Vector3(rotatedVertices[i][0].X, rotatedVertices[i][1].X, rotatedVertices[i][2].X);
            if (vert == numVertices)
            {
                break;
            }

            vertices[vert++] = new Vector3(rotatedVertices[i][0].Y, rotatedVertices[i][1].Y, rotatedVertices[i][2].Y);
            if (vert == numVertices)
            {
                break;
            }

            vertices[vert++] = new Vector3(rotatedVertices[i][0].Z, rotatedVertices[i][1].Z, rotatedVertices[i][2].Z);
            if (vert == numVertices)
            {
                break;
            }

            vertices[vert++] = new Vector3(rotatedVertices[i][0].W, rotatedVertices[i][1].W, rotatedVertices[i][2].W);
            if (vert == numVertices)
            {
                break;
            }
        }

        return vertices;
    }

    private static bool IsHullFaceValid(Span<Vector3> points, HullData hullData)
    {
        for (int faceIndex = 0; faceIndex < hullData.FaceStartIndices.Length; ++faceIndex)
        {
            hullData.GetFace(faceIndex, out var face);

            Vector3 faceNormal = default;
            var a = points[face[0]];
            var b = points[face[1]];
            var prev = b - a;

            for (int i = 2; i < face.VertexCount; ++i)
            {
                var c = points[face[i]];
                var curr = c - a;

                faceNormal += Vector3.Cross(prev, curr);
                prev = curr;
            }

            if (faceNormal.LengthSquared() <= 1e-20f)
            {
                return false;
            }
        }

        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private class PinZone
    {
        public string Name;

        // public ulong Timestamp
        // public uint GeneratedAt;
        public PinZoneChunk[] Chunks;
        public ENWFLayer[] Imports;
    }

    [StructLayout(LayoutKind.Sequential)]
    private class PinZoneChunk
    {
        public string Name;
        public Vector3 Origin;
    }

    [StructLayout(LayoutKind.Sequential)]
    private class PinChunk
    {
        public string Name;
        public PinChunkSubChunk[] SubChunks;
    }

    [StructLayout(LayoutKind.Sequential)]
    private class PinChunkSubChunk
    {
        public string Name;
        public ENWFLayer Cg;
        public ENWFLayer Cg2;
        public ENWFLayer Cg3;
    }
}