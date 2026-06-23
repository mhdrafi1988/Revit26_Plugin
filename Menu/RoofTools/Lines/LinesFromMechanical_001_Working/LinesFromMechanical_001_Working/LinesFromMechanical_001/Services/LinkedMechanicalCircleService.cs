using Autodesk.Revit.DB;
using Revit26_Plugin.LinesFromMechanical.V001_01.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.LinesFromMechanical.V001_01.Services;

public sealed class LinkedMechanicalCircleService
{
    public OperationSummary CreateCircles(
        Document hostDoc,
        ViewPlan activePlanView,
        double radiusFeet)
    {
        if (radiusFeet <= 0)
            throw new ArgumentOutOfRangeException(nameof(radiusFeet), "Circle radius must be greater than zero.");

        var summary = new OperationSummary();
        var processedSourceKeys = new HashSet<string>();
        var processedRoundedCenters = new HashSet<string>();

        IList<RevitLinkInstance> visibleLinkInstances = new FilteredElementCollector(hostDoc, activePlanView.Id)
            .OfClass(typeof(RevitLinkInstance))
            .WhereElementIsNotElementType()
            .Cast<RevitLinkInstance>()
            .ToList();

        using Transaction transaction = new(
            hostDoc,
            "Create Detail Circles at Linked Mechanical Equipment");

        transaction.Start();

        foreach (RevitLinkInstance linkInstance in visibleLinkInstances)
        {
            Document? linkDoc = linkInstance.GetLinkDocument();

            if (linkDoc == null)
            {
                summary.UnloadedLinksSkipped++;
                continue;
            }

            summary.LinkedModelsProcessed++;

            Transform linkTransform = linkInstance.GetTotalTransform();

            IList<Element> visibleLinkedMechanicalEquipment =
                CollectVisibleLinkedMechanicalEquipment(
                    hostDoc,
                    activePlanView,
                    linkInstance);

            summary.MechanicalEquipmentFound += visibleLinkedMechanicalEquipment.Count;

            foreach (Element linkedElement in visibleLinkedMechanicalEquipment)
            {
                if (!IsMechanicalEquipment(linkedElement))
                {
                    summary.SkippedElements++;
                    continue;
                }

                if (linkedElement.Location is not LocationPoint locationPoint)
                {
                    summary.SkippedElements++;
                    continue;
                }

                summary.ValidPointBasedFamilies++;

                string sourceKey = CircleIdentityStorage.BuildSourceKey(linkInstance, linkedElement);

                if (!processedSourceKeys.Add(sourceKey))
                {
                    summary.DuplicateElementsSkipped++;
                    continue;
                }

                if (CircleIdentityStorage.DetailCurveExistsForSource(hostDoc, activePlanView, sourceKey))
                {
                    summary.DuplicateElementsSkipped++;
                    continue;
                }

                XYZ hostPoint = linkTransform.OfPoint(locationPoint.Point);
                XYZ circleCenter = ProjectPointToViewPlane(hostPoint, activePlanView);

                string roundedCenterKey = BuildRoundedPointKey(circleCenter);

                if (!processedRoundedCenters.Add(roundedCenterKey))
                {
                    // Same transformed placement point is safe to skip.
                    summary.DuplicateElementsSkipped++;
                    continue;
                }

                int createdSegments = CreateDetailCircle(
                    hostDoc,
                    activePlanView,
                    circleCenter,
                    radiusFeet,
                    sourceKey);

                if (createdSegments > 0)
                    summary.CirclesCreated++;
                else
                    summary.SkippedElements++;
            }
        }

        transaction.Commit();

        return summary;
    }

    private static IList<Element> CollectVisibleLinkedMechanicalEquipment(
        Document hostDoc,
        View hostView,
        RevitLinkInstance linkInstance)
    {
        return new FilteredElementCollector(hostDoc, hostView.Id, linkInstance.Id)
            .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
            .WhereElementIsNotElementType()
            .ToElements();
    }

    private static bool IsMechanicalEquipment(Element element)
    {
        return element.Category != null
               && element.Category.Id.Value == (long)BuiltInCategory.OST_MechanicalEquipment;
    }

    private static XYZ ProjectPointToViewPlane(XYZ point, ViewPlan view)
    {
        Plane plane = Plane.CreateByNormalAndOrigin(
            view.ViewDirection,
            view.Origin);

        double signedDistance = plane.Normal.DotProduct(point - plane.Origin);

        return point - signedDistance * plane.Normal;
    }

    private static int CreateDetailCircle(
        Document doc,
        View view,
        XYZ center,
        double radius,
        string sourceKey)
    {
        XYZ xDirection = view.RightDirection.Normalize();
        XYZ yDirection = view.UpDirection.Normalize();

        double q0 = 0.0;
        double q1 = Math.PI / 2.0;
        double q2 = Math.PI;
        double q3 = Math.PI * 1.5;
        double q4 = Math.PI * 2.0;

        Arc[] arcs =
        [
            Arc.Create(center, radius, q0, q1, xDirection, yDirection),
            Arc.Create(center, radius, q1, q2, xDirection, yDirection),
            Arc.Create(center, radius, q2, q3, xDirection, yDirection),
            Arc.Create(center, radius, q3, q4, xDirection, yDirection)
        ];

        int created = 0;

        foreach (Arc arc in arcs)
        {
            DetailCurve detailCurve = doc.Create.NewDetailCurve(view, arc);
            CircleIdentityStorage.AttachSourceKey(detailCurve, sourceKey);
            created++;
        }

        return created;
    }

    private static string BuildRoundedPointKey(XYZ point)
    {
        const double tolerance = 0.0001;

        long x = (long)Math.Round(point.X / tolerance);
        long y = (long)Math.Round(point.Y / tolerance);
        long z = (long)Math.Round(point.Z / tolerance);

        return $"{x}|{y}|{z}";
    }
}