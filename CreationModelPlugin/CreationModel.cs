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

        public List <Wall> CreateWall(Document doc, Level level1, Level level2)
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

    }
}
