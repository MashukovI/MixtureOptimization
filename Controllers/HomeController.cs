using Microsoft.AspNetCore.Mvc;
using MixtureOptimization.Models;
using Google.OrTools.LinearSolver;
using System.Collections.Generic;
using System.Diagnostics;
using OfficeOpenXml;
using System.IO;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        var materials = new List<Material>
        {
            new Material { Name = "A1", Cost = 3.5, Parameters = new List<int> { 12, 12, 76, 76 } },
            new Material { Name = "A2", Cost = 3.5, Parameters = new List<int> { 20, 18, 62, 62 } },
            new Material { Name = "A3", Cost = 5.2, Parameters = new List<int> { 12, 18, 70, 70 } },
            new Material { Name = "A4", Cost = 4, Parameters = new List<int> { 20, 14, 54, 57 } },
            new Material { Name = "A5", Cost = 5, Parameters = new List<int> { 20, 14, 85, 75 } }
        };

        var substanceLimits = new List<int> { 15, 15, 50, 70 };
        ViewBag.LowerLimits = substanceLimits;
        return View(materials);
    }

    [HttpPost]
    public IActionResult Calculate(List<Material> materials, List<int> lowerLimits)
    {
        foreach (var material in materials)
        {
            Debug.WriteLine($"Received material: {material.Name}, Cost: {material.Cost}");
            if (string.IsNullOrEmpty(material.Name))
            {
                return Content("������: ���� �� ���� ���������� ������.");
            }
            if (material.Cost <= 0)
            {
                return Content("������: ��������� ��������� ������ ���� ������ ����.");
            }
            if (material.Parameters == null || material.Parameters.Count == 0)
            {
                return Content("������: ��������� ��������� �� ������.");
            }
            Debug.WriteLine($"Parameters: {string.Join(", ", material.Parameters)}");
        }

        Debug.WriteLine("Received Limits:");
        lowerLimits.ForEach(limit => Debug.WriteLine(limit.ToString()));

        Solver solver = Solver.CreateSolver("GLOP");
        if (solver == null)
        {
            return Content("������: �� ������� ������� ��������.");
        }

        int numMaterials = materials.Count;
        int numSubstances = lowerLimits.Count;

        Variable[] amounts = new Variable[numMaterials];
        for (int i = 0; i < numMaterials; i++)
        {
            amounts[i] = solver.MakeNumVar(0.0, 1.0, materials[i].Name);
        }

        Constraint weightConstraint = solver.MakeConstraint(1.0, 1.0, "TotalWeight");
        for (int i = 0; i < numMaterials; i++)
        {
            weightConstraint.SetCoefficient(amounts[i], 1.0);
        }

        for (int j = 0; j < numSubstances; j++)
        {
            Constraint substanceConstraint = solver.MakeConstraint(lowerLimits[j], double.PositiveInfinity, $"LowerLimit_{j}");
            for (int i = 0; i < numMaterials; i++)
            {
                substanceConstraint.SetCoefficient(amounts[i], materials[i].Parameters[j]);
            }
        }

        Objective objective = solver.Objective();
        for (int i = 0; i < numMaterials; i++)
        {
            objective.SetCoefficient(amounts[i], materials[i].Cost);
        }
        objective.SetMinimization();

        Solver.ResultStatus resultStatus = solver.Solve();
        if (resultStatus != Solver.ResultStatus.OPTIMAL)
        {
            return Content("������: ����������� ������� �� �������.");
        }

        var results = new List<Result>();
        double totalCost = 0;
        for (int i = 0; i < numMaterials; i++)
        {
            double amount = Math.Round(amounts[i].SolutionValue(), 3);
            double cost = Math.Round(amount * materials[i].Cost, 3);
            results.Add(new Result
            {
                Name = materials[i].Name,
                Amount = amount,
                Cost = cost
            });
            totalCost += cost;
        }
        totalCost = Math.Round(totalCost, 3);

        ViewBag.Results = results;
        ViewBag.TotalCost = totalCost;
        ViewBag.LowerLimits = lowerLimits;
        ViewBag.Materials = materials; 
        return View("Index", materials);
    }

    [HttpPost]
    public IActionResult ExportToExcel([FromForm] List<Material> materials, [FromForm] List<int> lowerLimits, [FromForm] List<Result> results)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using (var package = new ExcelPackage())
        {
            var worksheet = package.Workbook.Worksheets.Add("Results");

            // ���������
            worksheet.Cells[1, 1].Value = "�������� �������� / ��������";
            for (int i = 0; i < materials.Count; i++)
            {
                worksheet.Cells[1, i + 2].Value = materials[i].Name;
            }
            worksheet.Cells[1, materials.Count + 2].Value = "������ ������ (%)";

            // ��������� � ������ ������
            for (int j = 0; j < materials[0].Parameters.Count; j++)
            {
                worksheet.Cells[j + 2, 1].Value = $"B{j + 1} (�����)";
                for (int i = 0; i < materials.Count; i++)
                {
                    worksheet.Cells[j + 2, i + 2].Value = materials[i].Parameters[j];
                }
                worksheet.Cells[j + 2, materials.Count + 2].Value = lowerLimits[j];
            }

            // ���������
            worksheet.Cells[materials[0].Parameters.Count + 2, 1].Value = "��������� (�� ��)";
            for (int i = 0; i < materials.Count; i++)
            {
                worksheet.Cells[materials[0].Parameters.Count + 2, i + 2].Value = materials[i].Cost;
            }

            // ���������� �������
            var resultRow = materials[0].Parameters.Count + 3;
            worksheet.Cells[resultRow, 1].Value = "���������� �������";
            worksheet.Cells[resultRow + 1, 1].Value = "�������� ���������";
            worksheet.Cells[resultRow + 1, 2].Value = "���������� (��)";
            worksheet.Cells[resultRow + 1, 3].Value = "��������� (�.�.)";

            for (int i = 0; i < results.Count; i++)
            {
                worksheet.Cells[resultRow + 2 + i, 1].Value = results[i].Name;
                worksheet.Cells[resultRow + 2 + i, 2].Value = results[i].Amount;
                worksheet.Cells[resultRow + 2 + i, 3].Value = results[i].Cost;
            }
            worksheet.Cells[resultRow + 2 + results.Count, 1].Value = "����� ���������";
            worksheet.Cells[resultRow + 2 + results.Count, 3].Value = results.Sum(r => r.Cost);

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            var stream = new MemoryStream();
            package.SaveAs(stream);

            string fileName = "Results.xlsx";
            string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            stream.Position = 0;
            return File(stream, contentType, fileName);
        }
    }
}