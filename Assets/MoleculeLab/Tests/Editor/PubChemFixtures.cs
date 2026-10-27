namespace MoleculeLab.Tests
{
    // Verbatim PubChem PUG REST replies captured 26 October 2026, so tests never need the network.
    static class PubChemFixtures
    {
        public const string EthanolProperties = @"{
  ""PropertyTable"": {
    ""Properties"": [
      {
        ""CID"": 702,
        ""MolecularFormula"": ""C2H6O"",
        ""IUPACName"": ""ethanol"",
        ""Title"": ""Ethanol""
      }
    ]
  }
}
";
        public const string NotFoundProperties = @"{
  ""PropertyTable"": {
    ""Properties"": [
      {
        ""CID"": 0
      }
    ]
  }
}
";
        public const string BadRequest = @"{
  ""Fault"": {
    ""Code"": ""PUGREST.BadRequest"",
    ""Message"": ""Unable to standardize the given structure - perhaps some special characters need to be escaped or data packed in a MIME form?""
  }
}
";
        public const string EthanolSdf = @"702
  -OEChem-10262605403D

  9  8  0     0  0  0  0  0  0999 V2000
   -1.1712    0.2997    0.0000 O   0  0  0  0  0  0  0  0  0  0  0  0
   -0.0463   -0.5665    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
    1.2175    0.2668    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
   -0.0958   -1.2120    0.8819 H   0  0  0  0  0  0  0  0  0  0  0  0
   -0.0952   -1.1938   -0.8946 H   0  0  0  0  0  0  0  0  0  0  0  0
    2.1050   -0.3720   -0.0177 H   0  0  0  0  0  0  0  0  0  0  0  0
    1.2426    0.9307   -0.8704 H   0  0  0  0  0  0  0  0  0  0  0  0
    1.2616    0.9052    0.8886 H   0  0  0  0  0  0  0  0  0  0  0  0
   -1.1291    0.8364    0.8099 H   0  0  0  0  0  0  0  0  0  0  0  0
  1  2  1  0  0  0  0
  1  9  1  0  0  0  0
  2  3  1  0  0  0  0
  2  4  1  0  0  0  0
  2  5  1  0  0  0  0
  3  6  1  0  0  0  0
  3  7  1  0  0  0  0
  3  8  1  0  0  0  0
M  END
> <PUBCHEM_COMPOUND_CID>
702

$$$$
";
        public const string AceticAcidSdf = @"176
  -OEChem-10262605403D

  8  7  0     0  0  0  0  0  0999 V2000
   -0.3035    1.2890   -0.0002 O   0  0  0  0  0  0  0  0  0  0  0  0
   -0.9800   -0.8878   -0.0002 O   0  0  0  0  0  0  0  0  0  0  0  0
    1.3743   -0.3516   -0.0002 C   0  0  0  0  0  0  0  0  0  0  0  0
   -0.0907   -0.0496    0.0006 C   0  0  0  0  0  0  0  0  0  0  0  0
    1.8368    0.0570   -0.9021 H   0  0  0  0  0  0  0  0  0  0  0  0
    1.8400    0.0676    0.8952 H   0  0  0  0  0  0  0  0  0  0  0  0
    1.5207   -1.4356    0.0064 H   0  0  0  0  0  0  0  0  0  0  0  0
   -1.2598    1.5081   -0.0008 H   0  0  0  0  0  0  0  0  0  0  0  0
  1  4  1  0  0  0  0
  1  8  1  0  0  0  0
  2  4  2  0  0  0  0
  3  4  1  0  0  0  0
  3  5  1  0  0  0  0
  3  6  1  0  0  0  0
  3  7  1  0  0  0  0
M  END
";
        public const string BenzeneSdf = @"241
  -OEChem-10262605403D

 12 12  0     0  0  0  0  0  0999 V2000
   -1.2131   -0.6884    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
   -1.2028    0.7064    0.0001 C   0  0  0  0  0  0  0  0  0  0  0  0
   -0.0103   -1.3948    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
    0.0104    1.3948   -0.0001 C   0  0  0  0  0  0  0  0  0  0  0  0
    1.2028   -0.7063    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
    1.2131    0.6884    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
   -2.1577   -1.2244    0.0000 H   0  0  0  0  0  0  0  0  0  0  0  0
   -2.1393    1.2564    0.0001 H   0  0  0  0  0  0  0  0  0  0  0  0
   -0.0184   -2.4809   -0.0001 H   0  0  0  0  0  0  0  0  0  0  0  0
    0.0184    2.4808    0.0000 H   0  0  0  0  0  0  0  0  0  0  0  0
    2.1394   -1.2563    0.0001 H   0  0  0  0  0  0  0  0  0  0  0  0
    2.1577    1.2245    0.0000 H   0  0  0  0  0  0  0  0  0  0  0  0
  1  2  2  0  0  0  0
  1  3  1  0  0  0  0
  1  7  1  0  0  0  0
  2  4  1  0  0  0  0
  2  8  1  0  0  0  0
  3  5  2  0  0  0  0
  3  9  1  0  0  0  0
  4  6  2  0  0  0  0
  4 10  1  0  0  0  0
  5  6  1  0  0  0  0
  5 11  1  0  0  0  0
  6 12  1  0  0  0  0
M  END
";
        public static MoleculeLookupResult Found(int cid,string title,string iupac,string formula,string sdf) =>
            new MoleculeLookupResult { status=LookupStatus.Found, cid=cid, title=title, iupacName=iupac, molecularFormula=formula, sdf3d=sdf };
    }
}
