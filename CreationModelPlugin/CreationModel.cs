using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            #region "Полезное про фильтрацию элементов в модели"
            /*
            var res1 = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType)) //быстрый фильтр ревита
                //.Cast<Wall>()
                .OfType<WallType>() //метод расширения. фильтрация на основе заданного типа, тобишь он не преобразовывает Element в Wall, а забирает только типы Wall
                .ToList();

            var res2 = new FilteredElementCollector(doc)
               .OfClass(typeof(FamilyInstance)) //быстрый фильтр ревита
              .OfCategory(BuiltInCategory.OST_Doors)
               .OfType<FamilyInstance>() //метод расширения. фильтрация на основе заданного типа, тобишь он не преобразовывает Element в Wall, а забирает только типы Wall
               .Where(a=>a.Name.Equals("36\" x 84\""))
               .ToList();
            */
            #endregion

            Level level1 = GetLevel(doc) //нашли 1 уровень
            .Where(x => x.Name.Equals("Level 1"))
            .FirstOrDefault(); //т.к. уровень с таки названием будет только один в модели, то используем этот метод

            Level level2 = GetLevel(doc) //нашли 2 уровень
                .Where(x => x.Name.Equals("Level 2"))
                .FirstOrDefault(); //т.к. уровень с таки названием будет только один в модели, то используем этот метод

            Transaction transaction = new Transaction(doc, "Create wall");
            transaction.Start();

            List<Wall> wall = CreateWall(doc, level1, level2); //создали стены

            AddDoor(doc, level1, wall[0]);

            for (int i = 1; i < 4; i++)
            {
                AddWindow(doc, level1, wall[i]);
            }

            //AddRoof(doc, level2, wall);

            AddRoofByExtrusion(doc, level2, wall);

            transaction.Commit();

            return Result.Succeeded;
        }

        public List<Level> GetLevel(Document doc)
        {
            List<Level> listLevel = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .OfType<Level>()
                    .ToList();
            return listLevel;
        }

        public List<Wall> CreateWall(Document doc, Level level1, Level level2)
        {

            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters);
            double dx = width / 2;
            double dy = depth / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0)); //зацикливаем процесс

            List<Wall> walls = new List<Wall>();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, false);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
            }
            return walls;
        }

        public void AddDoor(Document doc, Level level1, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0915 x 2032mm"))
                .Where(x => x.FamilyName.Equals("M_Single-Flush"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            if (!doorType.IsActive)
                doorType.Activate();

            doc.Create.NewFamilyInstance(point, doorType, wall, level1, StructuralType.NonStructural);
        }

        public void AddWindow(Document doc, Level level1, Wall wall)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0610 x 1830mm"))
                .Where(x => x.FamilyName.Equals("M_Fixed"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            if (!windowType.IsActive)
                windowType.Activate();

            FamilyInstance window = doc.Create.NewFamilyInstance(point, windowType, wall, level1, StructuralType.NonStructural);

            double sillHeight = 1200;
            Parameter sillHeightParameter = window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
            sillHeightParameter.Set(UnitUtils.ConvertToInternalUnits(sillHeight, UnitTypeId.Millimeters));
        }

        public void AddRoof(Document doc, Level level2, List<Wall> wall)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>() //коллекция преобразовывается в коллекцию экземпляров RoofType
        .Where(XYZ => XYZ.Name.Equals("Cold Roof - Concrete"))
        .Where(x => x.FamilyName.Equals("Basic Roof"))
        .FirstOrDefault();

            double wallThikness = wall[0].Width;
            double dt = wallThikness / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dt, -dt, 0));
            points.Add(new XYZ(dt, -dt, 0));
            points.Add(new XYZ(dt, dt, 0));
            points.Add(new XYZ(-dt, dt, 0));
            points.Add(new XYZ(-dt, -dt, 0));

            Application application = doc.Application;
            CurveArray footPrint = application.Create.NewCurveArray(); // footptint отпечаток границы дома, по которой будет автоматически построена крыша

            for (int i = 0; i < 4; i++)
            {
                LocationCurve curve = wall[i].Location as LocationCurve;//у каждой стены берем свойство location и преобразуем его в LocationCurve, чтобы получить кривую
                XYZ p1 = curve.Curve.GetEndPoint(0);
                XYZ p2 = curve.Curve.GetEndPoint(1);
                Line line = Line.CreateBound(p1 + points[i], p2 + points[i + 1]);
                footPrint.Append(line);
            }
            ModelCurveArray footPrintToModelCurveMapping = new ModelCurveArray();
            FootPrintRoof footprintRoof = doc.Create.NewFootPrintRoof(footPrint, level2, roofType, out footPrintToModelCurveMapping);

            foreach (ModelCurve a in footPrintToModelCurveMapping)
            {
                footprintRoof.set_DefinesSlope(a, true);
                footprintRoof.set_SlopeAngle(a, 0.5);
            }
        }

        private void AddRoofByExtrusion(Document doc, Level level2, List<Wall> wall)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>() //коллекция преобразовывается в коллекцию экземпляров RoofType
        .Where(XYZ => XYZ.Name.Equals("Cold Roof - Concrete"))
        .Where(x => x.FamilyName.Equals("Basic Roof"))
        .FirstOrDefault();

            CurveArray curveArray = new CurveArray();

            LocationCurve curve0 = wall[0].Location as LocationCurve;//у каждой стены берем свойство location и преобразуем его в LocationCurve, чтобы получить кривую
            LocationCurve curve1 = wall[1].Location as LocationCurve;
            LocationCurve curve2 = wall[3].Location as LocationCurve;

            double elevation = level2.Elevation;

            XYZ p1 = curve0.Curve.GetEndPoint(0);
            XYZ p2 = curve1.Curve.GetEndPoint(1);
            XYZ p3 = curve2.Curve.GetEndPoint(0);
            XYZ p4 = new XYZ(p1.X, p1.Y - p2.Y, 10);

            double halfOfWallThikness = wall[0].Width / 2;

            curveArray.Append(Line.CreateBound(new XYZ(p1.X, p1.Y - halfOfWallThikness, p1.Z + elevation), new XYZ(p2.X, p1.Y+p2.Y, 18)));
            curveArray.Append(Line.CreateBound(new XYZ(p2.X, p1.Y + p2.Y, 18), new XYZ(p2.X, p2.Y + halfOfWallThikness, p2.Z + elevation)));

            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), doc.ActiveView);

            double startExtrusion = p2.X + halfOfWallThikness;
            double endExtrusion = p3.X - halfOfWallThikness;

            var roof = doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, startExtrusion, endExtrusion);
            roof.EaveCuts = EaveCutterType.TwoCutPlumb;
        }
    }
}
