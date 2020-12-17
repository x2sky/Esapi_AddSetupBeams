//////////////////////////////////////////////////////////////////////
///Class and functions to add setup beam
/// Include functions:
///     SetupBeam(Beam) - initialze function, creates BeamMachineParameters
///     AddBeam(PlanSetup, GantryAngle, PtOrientation, DRRParameters, BmNum) - add a setup beam to the plan based on gantry angle
///     CheckBeamDir(GantryAngle, PtOrientation) - Find beam orietation based on beam angle and patient orientation
///     
///--version 0.0
///Becket Hui 2020/12
//////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace addSetupBeams
{
    class SetupBeam
    {
        private ExternalBeamMachineParameters bmMachParam;
        private VVector plnIso;
        public SetupBeam(Beam currBm)
        {
            // Create machine parameters
            String energy = currBm.EnergyModeDisplayName;
            String fluence = null;
            Match EMode = Regex.Match(currBm.EnergyModeDisplayName, @"^([0-9]+[A-Z]+)-?([A-Z]+)?", RegexOptions.IgnoreCase);  //format is... e.g. 6X(-FFF)
            if (EMode.Success)
            {
                if (EMode.Groups[2].Length > 0)  // fluence mode
                {
                    energy = EMode.Groups[1].Value;
                    fluence = EMode.Groups[2].Value;
                } // else normal modes uses default in decleration
            }
            bmMachParam = new ExternalBeamMachineParameters(currBm.TreatmentUnit.Id.ToString(), energy, currBm.DoseRate, "STATIC", fluence);
            plnIso = currBm.IsocenterPosition;  // copy isocenter location
        }
        public void AddBeam(ExternalPlanSetup currPln, Double gantryAng, String ptOrient, DRRCalculationParameters drrParam, int bmIdNo)
        {
            if (Double.IsNaN(gantryAng))  // Gantry angle = Nan for CBCT
            {
                Beam bm = currPln.AddSetupBeam(bmMachParam, new VRect<double>(-50.0, -50.0, 50.0, 50.0), 0.0, 0.0, 0.0, plnIso);
                bm.Id = "C" + bmIdNo.ToString();
                bm.Name = "CBCT setup";
            }
            else  // kV beam
            {
                // adding 10 x 10 field, 0 coll rotation and 0 couch angle
                Beam bm = currPln.AddSetupBeam(bmMachParam, new VRect<double>(-50.0, -50.0, 50.0, 50.0), 0.0, gantryAng, 0.0, plnIso);
                String bmDir = CheckBeamDir(gantryAng, ptOrient);
                bm.Id = bmDir[0].ToString().ToUpper() + bmIdNo.ToString();
                bm.Name = bmDir + " kV setup";
                // create DRR
                bm.CreateOrReplaceDRR(drrParam);
            }
        }
        private String CheckBeamDir(Double gantryAng, String ptOrient)
        {
            switch (gantryAng)
            {
                case 0.0:
                    if (ptOrient == "HFS" || ptOrient == "FFS") return "anterior"; else return "posterior";
                case 270.0:
                    if (ptOrient == "HFS" || ptOrient == "FFP") return "right"; else return "left";
                case 90.0:
                    if (ptOrient == "HFS" || ptOrient == "FFP") return "left"; else return "right";
                default:
                    return "unknown";
            }
        }
    }
}
