بسم الله الرحمن الرحيم
# EP Analyzer - Evaluation Protocol Analyzer
[<img src="https://github.com/SwatInc/EPAnalyzer/blob/master/EPAnalyzer/Resources/e_icon.svg" width="100">](#)

`EP Analyzer` can be used by clinical laboratories to generate EP reports.

## Solution layout

| project | purpose |
|---|---|
| `EPAnalyzer` | WinForms/DevExpress UI (early prototype) |
| `EPAnalyzer.Statistics` | **all statistical formula engines** (precision, linearity, EP6, method comparison, EP9-A3, ROC, reference intervals, INR, trueness, QC, costs) |
| `TestingStatistics` | console harness: self-tests and worked-formula showcase |
| `docs/FORMULAS.md` | formula reference (math, CLSI basis and code mapping) |

Run the formula self-tests and showcase:

```
dotnet run --project TestingStatistics/TestingStatistics.csproj
```

## Implemented statistical engines

- **Descriptive**: weighted mean/SD/CV, χ² SD confidence intervals, SDI, verification values,
  order statistics/percentiles, robust statistics (median/MAD/Tukey biweight), normality tests.
- **Regression**: weighted OLS, Deming (closed form and iterative weighted Deming), Passing–Bablok
  with rank-based CIs, polynomial regression with lack-of-fit F test, jackknife CIs.
- **Precision**: Simple Precision, Complex Precision (EP5 nested ANOVA variance components with the
  5.5×SD outlier rule), EP10 grand statistics.
- **Linearity**: clinical wedge test with factor search, EP6 pooled SD/CV + Bartlett test + weighted
  polynomial regression, deviation from linearity, LOQ/functional sensitivity.
- **Method comparison**: AMC/EP9-A2/EP9-A3 statistics (bias, t/F/r, CIs), partitioned bias,
  constant SD/CV checks, precision-ratio routing, MDP helpers.
- **Detection**: LOB/LOD (EP17).
- **ROC**: AUC with DeLong variance/covariance, two-curve comparison, per-cutoff tables, exact and
  Wilson confidence intervals, kappa.
- **Reference intervals**: non-parametric/parametric/log-transformed limits with 90% CIs,
  verification, confidence ratio, Harris–Boyd partitioning, Dixon outliers.
- **INR**: geometric MNPT, INR calculation, manual INR check, calibration-verification error index.
- **Trueness**: bias/uncertainty, Ricos biological-variation goals, Six Sigma metric.
- **QC**: carryover, EP7 interference, stability; **Costs**: cost-per-test arithmetic.

# Current Development Status
This is still very early and none of the features listed below are implemented yet. You should be able to `pre-releases` from `releases` tab as they are released.

If you have good knowledge of statistics and you are intrested in the project, please test as releases are published and create detailed issues in the issue tracker.

# Planned Modules

## Precision
- Simple Precision
- Complex Precision

# Accuracy and Linearity
- Linearity and Calibration Verification
- Simple Accuracy
- EP6 Linearity
- Trueness

# Method Comparision
...

# Sensitivity

# Reference Interval
- Verification of Reference Interval
- Establishing Reference Interval

# Coagulation
- INR Geometeric Mean and Verification of Reference Interval
- ...

and other modules depending on time and need.
