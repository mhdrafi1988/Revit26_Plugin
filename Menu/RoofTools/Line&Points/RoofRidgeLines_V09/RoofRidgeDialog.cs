using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08
    public partial class RoofRidgeDialog : Form
    {
        private UIDocument _uiDoc;
        private ElementId _roofId;
        private XYZ _point1, _point2;

        private enum ToolState { SelectRoof, PickPoint1, PickPoint2, Process, Complete }
        private ToolState _currentState;

        private Label _statusLabel;
        private Button _actionButton;
        private Button _cancelButton;
        private Label _resultLabel;

        public RoofRidgeDialog(UIDocument uiDoc)
        {
            _uiDoc = uiDoc;
            InitializeDialog();
            SetState(ToolState.SelectRoof);
        }

        private void InitializeDialog()
        {
            // Dialog properties
            this.Text = "Roof Ridge Tool";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ClientSize = new Size(350, 180);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;

            // Status label
            _statusLabel = new Label
            {
                Location = new Point(20, 20),
                Size = new Size(310, 40),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10, FontStyle.Regular)
            };

            // Result label
            _resultLabel = new Label
            {
                Location = new Point(20, 70),
                Size = new Size(310, 40),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.DarkGreen,
                Visible = false
            };

            // Action button
            _actionButton = new Button
            {
                Location = new Point(140, 120),
                Size = new Size(90, 30),
                Text = "Next",
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };
            _actionButton.Click += OnActionButtonClick;

            // Cancel button
            _cancelButton = new Button
            {
                Location = new Point(240, 120),
                Size = new Size(90, 30),
                Text = "Cancel",
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                DialogResult = DialogResult.Cancel
            };
            _cancelButton.Click += (s, e) => this.Close();

            // Add controls
            this.Controls.Add(_statusLabel);
            this.Controls.Add(_resultLabel);
            this.Controls.Add(_actionButton);
            this.Controls.Add(_cancelButton);
        }

        private void SetState(ToolState newState)
        {
            _currentState = newState;
            _resultLabel.Visible = false;

            switch (newState)
            {
                case ToolState.SelectRoof:
                    _statusLabel.Text = "Step 1/4: Select a roof element";
                    _actionButton.Text = "Select Roof";
                    break;

                case ToolState.PickPoint1:
                    _statusLabel.Text = "Step 2/4: Pick first point";
                    _actionButton.Text = "Pick Point";
                    break;

                case ToolState.PickPoint2:
                    _statusLabel.Text = "Step 3/4: Pick second point";
                    _actionButton.Text = "Pick Point";
                    break;

                case ToolState.Process:
                    _statusLabel.Text = "Step 4/4: Ready to process";
                    _actionButton.Text = "Process";
                    break;

                case ToolState.Complete:
                    _statusLabel.Text = "Processing complete";
                    _actionButton.Text = "Close";
                    _resultLabel.Visible = true;
                    break;
            }
        }

        private void OnActionButtonClick(object sender, EventArgs e)
        {
            try
            {
                this.Enabled = false;

                switch (_currentState)
                {
                    case ToolState.SelectRoof:
                        SelectRoof();
                        break;

                    case ToolState.PickPoint1:
                        PickPoint(ref _point1, ToolState.PickPoint2);
                        break;

                    case ToolState.PickPoint2:
                        PickPoint(ref _point2, ToolState.Process);
                        break;

                    case ToolState.Process:
                        ProcessRoof();
                        break;

                    case ToolState.Complete:
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                        break;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled picking - stay in current state
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Roof Ridge Tool",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Enabled = true;
            }
        }

        private void SelectRoof()
        {
            Reference roofRef = _uiDoc.Selection.PickObject(
                ObjectType.Element,
                new RoofSelectionFilter(),
                "Select a roof element");

            _roofId = roofRef.ElementId;
            SetState(ToolState.PickPoint1);
        }

        private void PickPoint(ref XYZ storePoint, ToolState nextState)
        {
            storePoint = _uiDoc.Selection.PickPoint("Pick point");
            SetState(nextState);
        }

        private void ProcessRoof()
        {
            using (Transaction trans = new Transaction(_uiDoc.Document, "Create Roof Ridge"))
            {
                trans.Start();

                try
                {
                    // Get the roof element
                    RoofBase roof = _uiDoc.Document.GetElement(_roofId) as RoofBase;
                    if (roof == null)
                        throw new InvalidOperationException("Selected roof not found");

                    // Enable shape editing if not already enabled
                    if (!ShapeEditor.IsEnabled(roof))
                        ShapeEditor.EnableShapeEditing(roof);

                    // YOUR PROCESSING LOGIC HERE
                    // This is where you implement your ridge line creation
                    // using _point1 and _point2

                    // Example: Create a model line between points
                    Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, _point1);
                    SketchPlane sketchPlane = SketchPlane.Create(_uiDoc.Document, plane);

                    ModelLine ridgeLine = _uiDoc.Document.Create.NewModelCurve(
                        Line.CreateBound(_point1, _point2),
                        sketchPlane) as ModelLine;

                    trans.Commit();

                    // Show success summary
                    _resultLabel.Text = $"Success!\n" +
                                       $"Roof: {roof.Id}\n" +
                                       $"Points: ({_point1.X:F2}, {_point1.Y:F2}) to ({_point2.X:F2}, {_point2.Y:F2})";

                    SetState(ToolState.Complete);
                }
                catch
                {
                    trans.RollBack();
                    throw;
                }
            }
        }
    }
}