using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MoleculeLab.Tests
{
    public sealed class MoleculeInfoTests
    {
        // Description reply for CID 702 as returned by PubChem on 27 October 2026.
        const string EthanolDescription=@"{""InformationList"":{""Information"":[{""CID"":702,""Title"":""Ethanol""},
{""CID"":702,""Description"":""Ethyl alcohol in alcoholic beverages can cause developmental toxicity according to an independent committee of scientific and health experts."",""DescriptionSourceName"":""California Office of Environmental Health Hazard Assessment (OEHHA)""},
{""CID"":702,""Description"":""Ethanol is a primary alcohol that is ethane in which one of the hydrogens is substituted by a hydroxy group. It has a role as a central nervous system depressant, an antiseptic drug, a disinfectant, a polar solvent, a teratogenic agent, a neurotoxin, a NMDA receptor antagonist, a protein kinase C agonist, a mouse metabolite, a Saccharomyces cerevisiae metabolite, an Escherichia coli metabolite and a human metabolite. It is a volatile organic compound, a primary alcohol, a member of ethanols and an alkyl alcohol. It is a conjugate acid of an ethoxide."",""DescriptionSourceName"":""ChEBI""}]}}";
        // Abridged PUG View "Uses" record modelled on CID 176 (acetic acid): same structure, noise entries kept.
        const string AceticAcidUses=@"{""Record"":{""RecordType"":""CID"",""RecordNumber"":176,""Section"":[{""TOCHeading"":""Use and Manufacturing"",""Section"":[{""TOCHeading"":""Uses"",
""Information"":[
{""Name"":""Cosmetic Ingredient Review Link"",""Value"":{""StringWithMarkup"":[{""String"":""CIR ingredient: Acetic Acid""}]}},
{""Name"":""Sources/Uses"",""Value"":{""StringWithMarkup"":[{""String"":""Used to manufacture acetic anhydride and other organic chemicals used in the plastic, pharmaceutical, dye, insecticide, textile, rubber, and photographic industries; [ACGIH]""}]}},
{""Name"":""Industrial Processes with risk of exposure"",""Value"":{""StringWithMarkup"":[{""String"":""Semiconductor Manufacturing [Category: Industry]""}]}},
{""Value"":{""StringWithMarkup"":[{""String"":""For acetic acid (USEPA/OPP Pesticide Code: 044001) ACTIVE products with label matches. /SRP: Registered for use in the U.S.""}]}},
{""Value"":{""StringWithMarkup"":[{""String"":""Used as a laboratory reagent in chemical and biochemical analysis, in field testing of lead fumes, vinyl chloride determination.""}]}},
{""Value"":{""StringWithMarkup"":[{""String"":""For more Uses (Complete) data for ACETIC ACID (19 total), please visit the HSDB record page.""}]}},
{""Name"":""CPDat Function Use Categories"",""Value"":{""ExternalTableName"":""cpdatfun""}}],
""Section"":[{""TOCHeading"":""Use Classification"",""Information"":[{""Value"":{""StringWithMarkup"":[{""String"":""Food additives""}]}}]}]}]}]}}";
        const string ClassificationOnly=@"{""Record"":{""Section"":[{""TOCHeading"":""Use and Manufacturing"",""Section"":[{""TOCHeading"":""Uses"",""Section"":[{""TOCHeading"":""Use Classification"",""Information"":[
{""Value"":{""StringWithMarkup"":[{""String"":""EPA Safer Chemical Functional Use Classes -> Solvents""}]}},
{""Value"":{""StringWithMarkup"":[{""String"":""Safer Chemical Classes ->  Green circle - The chemical has been verified to be of low concern""}]}},
{""Value"":{""StringWithMarkup"":[{""String"":""Fragrance Ingredients""}]}}]}]}]}]}}";

        [Test] public void DescriptionPrefersChebiAndIsShortened()
        {
            var (description,source)=PubChemClient.ParseDescription(EthanolDescription);
            Assert.That(source,Is.EqualTo("ChEBI"));
            Assert.That(description,Does.StartWith("Ethanol is a primary alcohol"));
            Assert.That(description.Length,Is.LessThanOrEqualTo(333));
            Assert.That(PubChemClient.ParseDescription(@"{""InformationList"":{""Information"":[{""CID"":5,""Title"":""X""}]}}").description,Is.Null);
            Assert.That(PubChemClient.ParseDescription("<html>").description,Is.Null);
        }
        [Test] public void UsesKeepReadableSentencesAndSkipNoise()
        {
            var uses=PubChemClient.ParseUses(AceticAcidUses);
            Assert.That(uses.Length,Is.EqualTo(2));
            Assert.That(uses[0],Does.StartWith("Used to manufacture acetic anhydride")); Assert.That(uses[0],Does.Not.Contain("[ACGIH]"));
            Assert.That(uses[1],Does.StartWith("Used as a laboratory reagent"));
            Assert.That(uses.Any(u=>u.Contains("CIR") || u.Contains("Category") || u.Contains("HSDB")),Is.False);
        }
        [Test] public void UsesFallBackToClassification()
        {
            Assert.That(PubChemClient.ParseUses(ClassificationOnly),Is.EqualTo(new[]{"Classified as: solvents, fragrance ingredients."}));
            Assert.That(PubChemClient.ParseUses(@"{""Record"":{""Section"":[]}}"),Is.Empty);
            Assert.That(PubChemClient.ParseUses("not json"),Is.Empty);
        }
        [Test] public void ShortenKeepsWholeSentences()
        {
            Assert.That(PubChemClient.Shorten("One two. Three four five six seven.",15),Is.EqualTo("One two."));
            Assert.That(PubChemClient.Shorten("short",50),Is.EqualTo("short"));
            Assert.That(PubChemClient.Shorten("alpha beta gamma delta epsilon",14),Is.EqualTo("alpha beta..."));
            Assert.That(PubChemClient.UsesUrl(702),Does.EndWith("pug_view/data/compound/702/JSON?heading=Uses"));
        }
        [Test] public void ReleasedAtomDoesNotDragTheStructureAlong()
        {
            var s=new FreeStructure();
            var c=s.AddAtom("C",4,Vector3.zero,-1); var c2=s.AddAtom("C",4,Vector3.right*FreeStructure.HeavyBond,-1); s.AddBond(c,c2);
            s.SetPosition(c2,new Vector3(.6f,.3f,0)); // pulled far towards the hand, then let go
            s.Relax(140,-1,c2);
            Assert.That(s.Position(c).magnitude,Is.LessThan(.0001f),"The atom that stayed put keeps its place.");
            Assert.That(Vector3.Distance(s.Position(c),s.Position(c2)),Is.EqualTo(FreeStructure.HeavyBond).Within(.005f),"The released atom springs back.");
        }
    }
}
