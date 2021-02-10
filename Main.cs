//////////////////////////////////////////////////////////////////////
///This program adds 4 kV setup beams and 1 CBCT setup beam to the current plan
///
///--version 1.0.0.3
///Becket Hui 2021/1
///  Add 180 kV beam
///  
///--version 1.0.0.2
///Becket Hui 2021/1
///  Add exception for plan that is approved/reviewed
///
///--version 1.0.0.1
///Becket Hui 2020/12
//////////////////////////////////////////////////////////////////////
using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using addSetupBeams;
using System.Text.RegularExpressions;
using System.Diagnostics;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.3")]
[assembly: AssemblyFileVersion("1.0.0.3")]
[assembly: AssemblyInformationalVersion("1.0")]

// TODO: Uncomment the following line if the script requires write access.
[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        public Script()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context)
        {
            // First check version of the ESAPI dll
            String esapiVer = FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(ExternalPlanSetup)).Location).FileVersion;
            Match mEsapiVer = Regex.Match(esapiVer, @"^(\d+).");
            if (mEsapiVer.Success)
            {
                int esapiVer0 = Int32.Parse(mEsapiVer.Groups[1].Value);
                if (esapiVer0 < 16)
                    throw new ApplicationException("ESAPI ver." + esapiVer + ", script cannot run on version below 16.");
            }

            // Open current patient
            Patient currPt = context.Patient;
            // If there's no selected patient, throw an exception
            if (currPt == null)
                throw new ApplicationException("Please open a patient before using this script.");

            // Open current plan
            ExternalPlanSetup currPln = context.ExternalPlanSetup;
            // If there's no selected plan, throw an exception
            if (currPln == null)
                throw new ApplicationException("Please select a plan before using this script.");

            // Check if plan is approved
            if (currPln.ApprovalStatus != PlanSetupApprovalStatus.UnApproved)
                throw new ApplicationException("Please unapprove plan before using this script.");

            // Get first beam
            Beam currBm = currPln.Beams.FirstOrDefault();
            // If there's no beam in plan, throw an exception
            if (currBm == null)
                throw new ApplicationException("Please add a beam with valid isocenter in current plan before using this script.");

            // Get patient orientation
            String ptOrient = "";
            switch (currPln.TreatmentOrientation)
            {
                case PatientOrientation.HeadFirstSupine:
                    ptOrient = "HFS";
                    break;
                case PatientOrientation.HeadFirstProne:
                    ptOrient = "HFP";
                    break;
                case PatientOrientation.FeetFirstSupine:
                    ptOrient = "FFS";
                    break;
                case PatientOrientation.FeetFirstProne:
                    ptOrient = "FFP";
                    break;
                default:
                    ptOrient = "UK";
                    break;
            }
            if (ptOrient == "UK")
                throw new ApplicationException("Cannot determine beam orientation relative to patient orientation, no setup beam created.");

            // Create DRR parameters
            DRRCalculationParameters drrParam = new DRRCalculationParameters(500);  // 50 cm DRR size
            if (currPln.Id.ToUpper().Contains("BREAST"))  // breast plan uses chest DRR setting
            {
                drrParam.SetLayerParameters(0, 0.6, -990.0, 0.0, 1.0, 5.0);
                drrParam.SetLayerParameters(1, 0.1, -450.0, 0.0, -4.0, 8.0);
                drrParam.SetLayerParameters(2, 1.0, 100.0, 1000.0);
            }
            else  // all other plan uses the default DRR setting
            {
                drrParam.SetLayerParameters(0, 2.0, 0.0, 130.0, -100.0, 100.0);
                drrParam.SetLayerParameters(1, 10.0, 100.0, 1000.0, -100.0, 100.0);
            }

            // Determine beam number based on the largest number currently in the setup beam ID under the current patient
            int bmIdNo = 0;
            foreach (Course crs in currPt.Courses)
            {
                foreach (ExternalPlanSetup pln in crs.ExternalPlanSetups)
                {
                    foreach (Beam bm in pln.Beams)
                    {
                        Match bmId = Regex.Match(bm.Id, @"^[A-Z](\d+)$");
                        if (bmId.Success)
                        {
                            bmIdNo = Math.Max(bmIdNo, Int32.Parse(bmId.Groups[1].Value));
                        }
                    }
                }
            }
            bmIdNo = bmIdNo + 1;

            // Create setup beam
            currPt.BeginModifications();
            SetupBeam setupBm = new SetupBeam(currBm);
            setupBm.AddBeam(currPln, Double.NaN, ptOrient, drrParam, bmIdNo);  // CBCT
            setupBm.AddBeam(currPln, 0.0, ptOrient, drrParam, bmIdNo);
            setupBm.AddBeam(currPln, 270.0, ptOrient, drrParam, bmIdNo);
            setupBm.AddBeam(currPln, 90.0, ptOrient, drrParam, bmIdNo);
            setupBm.AddBeam(currPln, 180.0, ptOrient, drrParam, bmIdNo);

            MessageBox.Show("Set of setup beam has been created in plan " + currPln.Id + ".");
        }
    }
}
