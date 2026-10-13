# EP Analyzer — statistical formula reference

This document describes every formula implemented in **`EPAnalyzer.Statistics`** and maps it to
the CLSI/CLIA protocol it serves. It is the mathematical companion to the code; each section lists
the implementing class and the module(s) that consume it.

Notation: `x̄`/`ȳ` are means, `s` a sample standard deviation, `n` the sample size, `w` a weight,
`Σw` the sum of weights, `df` degrees of freedom, `z` the standard-normal quantile, `t(α, df)` the
student-t quantile, `χ²(α, df)` the chi-square quantile. All confidence intervals default to 95%
unless stated otherwise.

## 1. Descriptive statistics

### 1.1 Weighted accumulator — `Descriptive.StatUnit`

| quantity | formula |
|---|---|
| mean | `x̄_w = Σwx / Σw` |
| variance | `s² = (Σwx² − (Σwx)²/Σw) / (n − 1)` |
| standard deviation | `s = √s²` |
| standard error of the mean | `SEM = s/√n` |
| coefficient of variation | `CV = 100·s/|x̄|` |
| 95% half-width of the mean | `D95 = t(0.95, n−1)·SEM` |
| standard deviation index | `SDI(x) = (x − x̄)/s` |
| SD confidence factors | `√((n−1)/χ²(1−α/2, n−1)) … √((n−1)/χ²(α/2, n−1))` |
| SD confidence interval | `s·factor` for the two factors above (χ² method) |
| verification value (one-sided upper SD limit) | `s·√(χ²(confidence, n−1)/(n−1))` |

### 1.2 Order statistics and robustness — `Descriptive.StatUnitEx`

- Percentile (`PERCENTILE.INC` convention): rank `r = p/100·(n−1)`, linear interpolation between
  the bracketing order statistics.
- Median = 50th percentile; `IQR = P75 − P25`.
- Skewness `g1 = m3/m2^(3/2)`; kurtosis `m4/m2²`; Fisher corrections `G1`, `G2`.
- MAD = median of `|x − median|`; normal-consistent `SMAD = MAD/0.6745`.
- Geometric mean `exp(mean(ln x))`, geometric SD `exp(SD(ln x))`, geometric `CV = 100·√(exp(σ²_ln)−1)`.
- Percentile confidence ranks (CLSI C28 "exact formula"): lower rank is the largest `r` with
  `P(X ≤ r−1) ≤ α/2`, upper rank is the smallest `s` with `P(X ≥ s) ≤ α/2`, `X ~ Bin(n, p)`.
  The table option uses `n·p ± z·√(n·p·(1−p))`.

### 1.3 Robust (Tukey biweight) — `Descriptive.RobustStatistics`

- Location/scale iteration with `u = (x − c)/(9s)`, weights `w = (1−u²)²` for `|u| < 1`:
  `c' = c + Σ(x−c)w/Σw`; the scale is updated from the weighted sums of `(x−c)²(1−5u²)(1−u²)`.
- Convergence tolerance `1e-4`, seeded with the median and the SMAD.

## 2. Probability toolkit — `Distributions.StatDistributions`

- Student `t`, `χ²`, `F`, normal CDF and quantiles.
- Binomial CDF via the regularized incomplete beta: `P(X ≤ k) = I_{1−p}(n−k, k+1)`.
- Exact (Clopper–Pearson) proportion interval:
  `lower = BetaInv(α/2; x, n−x+1)` (0 when `x = 0`),
  `upper = BetaInv(1−α/2; x+1, n−x)` (1 when `x = n`).
- Wilson score interval:
  `center = (p + z²/2n)/(1+z²/n)`, `half = z·√(p(1−p)/n + z²/4n²)/(1+z²/n)`.
- Agresti–Coull interval: `ñ = n + z²`, `p̃ = (x + z²/2)/ñ`,
  `p̃ ± z·√(p̃(1−p̃)/ñ)`.
- Anderson–Darling normality (mean/SD estimated): `A² = −n − (1/n)Σ(2i−1)[ln z_i + ln(1−z_{n+1−i})]`
  with `z_i = Φ((x_i − x̄)/s)`; p-value by the D'Agostino–Stephens approximation.
- F tail at zero/positive infinity are defined as 1 and 0 so lack-of-fit tests never return NaN.

## 3. Regression engines

### 3.1 Weighted sums — `Regression.WeightedRegressionAccumulator`

```
Σw, Σwx, Σwy, Σwx², Σwy², Σwxy, Σ(y−x), Σ(y−x)², n
Sxx = Σwx² − (Σwx)²/Σw      Var(X) = Sxx/(Σw − 1)
Syy = Σwy² − (Σwy)²/Σw      Var(Y) = Syy/(Σw − 1)
Sxy = Σwxy − (Σwx)(Σwy)/Σw  r = Sxy/√(Sxx·Syy)
```

Derived method-comparison quantities:
`bias = ȳ − x̄`; `%bias = 100·bias/x̄` (or midpoint `(x̄+ȳ)/2`);
`SD_diff = √((Σd² − (Σd)²/n)/(n−1))`, `d = y − x`; `SE_diff = SD_diff/√n`;
`df = n − 2`; `t = bias/SE_diff`; `F = max(VarX, VarY)/min(VarX, VarY)`.

### 3.2 OLS — `RegressionMath.Ols`

`b = Sxy/Sxx`, `a = ȳ_w − b·x̄_w`.

### 3.3 Deming — `RegressionMath.Deming`

With the error ratio `λ = (s_y/s_x)²`:

```
b = (Syy − λ·Sxx + √((Syy − λ·Sxx)² + 4λ·Sxy²)) / (2·Sxy)
```

the sign of `b` is forced to `sign(Sxy)`; `a = ȳ − b·x̄`; falls back to OLS when the solution is
unusable. `λ → ∞` reproduces OLS, `λ = 1` is the equal-error case.

Residual and standard errors (`RegressionMath.PostCalc`):

```
SEE²   = (b²Sxx − 2bSxy + Syy)/(n − 2)
SE_b   = SEE/√Sxx
SE_a   = SEE·√(1/Σw + x̄²/Sxx)
CI     = estimate ± t(0.95, n−2)·SE
```

### 3.4 Weighted Deming (Linnet proportional-error) — `RegressionMath.WeightedDeming`

Iterate to convergence:

```
m = (X + λY)/(1 + λ)               (first pass, from the raw pairs)
w = 1/m²
fit Deming with weights w
Ŷ = a + bX ;  X̂ = (Y − a)/b
m = (X̂ + λŶ)/(1 + λ) ;  w = 1/m²   (subsequent passes)
```

### 3.5 Prediction / confidence intervals of the line

```
ŷ = a + b·x
prediction interval: ŷ ± t·SEE·√(1 + 1/n + (x − x̄)²/Sxx)
mean interval:       ŷ ± t·SEE·√(1/n + (x − x̄)²/Sxx)
```

### 3.6 Jackknife — `RegressionMath.Jackknife`

For an estimator `θ` (slope/intercept):

```
θ*_i = n·θ_full − (n−1)·θ_(−i)
θ̄*  = Σθ*_i / n
SE   = √( Σ(θ*_i − θ̄*)² / (n(n−1)) )
CI   = θ* ± t(0.95, n−2)·SE
```

### 3.7 Passing–Bablok — `Regression.PassingBablok`

- Pairwise slopes `s_ij = (y_j − y_i)/(x_j − x_i)` for `x_i ≠ x_j`; **slope = median of the slopes**.
- **Intercept = median of `y_i − b·x_i`**.
- 95% slope CI: `k = ⌊z·√(n(n−1)(2n+5)/18)⌋` with `n` = number of data points; the sorted slopes of
  count `N = n(n−1)/2` give `s_(k+1)` (lower) and `s_(N−k)` (upper).
- Intercept CI from the medians of `y − b·x` at the two slope CI endpoints.

### 3.8 Weighted polynomial regression with lack-of-fit — `Regression.PolynomialRegression`

Normal equations `(XᵀWX)β = XᵀWy` solved for `β = (β₀ … β_d)`; with replicate groups:

```
SS_resid = Σ w(y − ŷ)²
SS_pure  = Σ_groups Σ_i (y_i − ȳ_group)²      df_pure = Σ(k_g − 1)
SS_LoF   = SS_resid − SS_pure                 df_LoF  = (n − (d+1)) − df_pure
MS_LoF   = SS_LoF/df_LoF ;  MS_pure = SS_pure/df_pure
F        = MS_LoF/MS_pure ;  p = P(F_{df_LoF,df_pure} > F)
R²       = 1 − SS_resid/SS_tot
SE(β_j)  = √(inv(XᵀWX)_jj · MS_resid)
CI(x)    = ŷ ± t(0.95, df_resid)·√(basisᵀ·inv(XᵀWX)·basis)·√MS_resid
PI(x)    = ŷ ± t·√((1 + basisᵀ·inv(XᵀWX)·basis)·MS_resid)
```

`BestDegree` picks the lowest degree (1…max) whose lack-of-fit is not significant (p ≥ 0.05).
Zero pure error with non-zero residual variance means all variation is lack of fit (F = ∞, p = 0).

## 4. Method comparison (AMC / EP9-A2 / EP9-A3)

`MethodComparison.MethodComparisonStats` exposes the accumulator quantities of §3.1 and fits any
of the five models: OLS, weighted OLS (`w = 1/x²`), Deming, weighted Deming, Passing–Bablok.
Bias CI: `bias ± t(0.95, n−1)·SE_diff`.

`MethodComparison.Ep9A3Regressions` adds:

- `UnweightedDeming`, `WeightedDeming`, `WeightedOls` variants;
- jackknife slope/intercept SE and CI (df = n−2);
- **constant-SD check**: split the X range into thirds, ratio of the low-third to high-third
  residual dispersion must lie in `[0.5, 2.0]`;
- **constant-CV check**: same ratio; a ratio > 4 raises the extreme flag;
- **average bias** = median of the per-pair differences;
- **partitioned bias** per partition: mean difference with its t CI;
- **precision ratio** = `s_x/s_y`; uniform scatter when `0.33 ≤ ratio ≤ 3`;
- the EP9 rule requiring `≥ 39` used pairs for a non-preliminary report.

## 5. ANOVA — `MethodComparison.Anova`

One-way ANOVA:

```
SS_B = Σ n_i(x̄_i − x̄)² ,  SS_W = ΣΣ(x_ij − x̄_i)² ,  SS_T = SS_B + SS_W
df_B = k−1 ,  df_W = N−k ,  MS = SS/df ,  F = MS_B/MS_W
```

Pooled SD `√MS_W`, pooled CV `100·pooled SD/|grand mean|`; EP26 mean comparison limit
`z·√(MSE·(1/n₁ + 1/n₂))`; SD comparison by the F ratio with `df` degrees of freedom in both tails.

## 6. Precision

### 6.1 Simple precision — `Precision.SimplePrecision`

Mean, SD, CV, χ² SD CI, SDI, verification values (§1.1). Goal modes:

| mode | SD goal |
|---|---|
| TEa-based | `(random-error budget %/100)·TEa_conc` or `(budget %/100)·TEa% /100·|mean|` |
| vendor SD | the vendor SD |
| vendor random-error allowable | the allowable concentration or `(%/100)·|mean|` |

Classification: pass when the whole SD CI lies below the goal, fail when it lies above, otherwise
uncertain; preliminary when `n < 3`.

The existing `TestingStatistics/SimplePrecision.cs` example produced the χ² interval
`[0.52, 6.29]·s` for `n = 3`, matching the χ² factors.

### 6.2 Complex precision / EP5 — `Precision.ComplexPrecision`

Nested design: day → run → replicate. For a balanced design:

```
s²_WR = MS_within-run
s²_BR = (MS_between-runs − MS_within-run)/k            k = replicates per run
s²_BD = (MS_between-days − MS_between-runs)/(k·r)      r = runs per day
s²_TOT = s²_WR + s²_BR + s²_BD
```

The implemented ANOVA table also handles unbalanced data through weighted SS.
Outlier rule: maximum acceptable replicate difference = `5.5 · s_prelim` where
`s_prelim = √(Σd²/2m)` over replicate differences; an offending day is excluded.
Verification value `s·√(χ²(confidence, df)/df)`; Satterthwaite df for the total variance.

### 6.3 EP10 — `Precision.Ep10`

```
grand mean     = mean(run × run means)
grand variance = mean(run variances)
grand SD       = √grand variance
grand CV       = 100·grand SD/|grand mean|
bias           = grand mean − assigned value
```

Pass rules: `|bias| ≤ bias limit`, `CV ≤ CV limit`; a final report needs more than four valid runs
and all levels passing. Scale factors: assigned difference (`high − low`) and ratio (`high/low`).

## 7. Linearity

### 7.1 Clinical linearity wedge — `Linearity.ClinicalLinearity`

Each specimen has an assigned `X`, a recovered value `v1`, and an error estimate `v2`.
For a factor `f`, the error bars are `u_i = v1_i + f·v2_i`, `l_i = v1_i − f·v2_i`.
A straight line meets all bars iff `max(slope_lo) ≤ min(slope_hi)` with, for each pair `i < j`
and `ΔX = X_j − X_i ≠ 0`:

```
slope_lo(i,j) = (l_j − u_i)/ΔX
slope_hi(i,j) = (u_j − l_i)/ΔX
```

The smallest factor is found by doubling (≤50 times) followed by 50 bisection steps. Recovery
`%rec = 100·value/defined`; allowed-SD interpolation between control levels; proportional
TE/SE/RE bands `0.01·pct·x`; accuracy pass when `|measured − defined| ≤ SE limit + 0.5·10^(−dp)`;
precision pass when `SD ≤ RE limit`; precision index `SD/RE limit`.

### 7.2 EP6 linearity — `Linearity.Ep6Linearity`

Pooled statistics and Bartlett homogeneity test:

```
s²_pooled = Σ df_i·s_i² / Σ df_i
CV_pooled = 100·√( Σ df_i·CV_i² / Σ df_i )
M = (Σ df)·ln(s²_pooled) − Σ df_i·ln(s_i²)
C = 1 + (Σ 1/df_i − 1/Σ df_i) / (3(k−1))
χ² = M/C   with k−1 df ;  heterogeneous when p < 0.05
```

When heterogeneity is flagged the polynomial regression is weighted by `w = 1/s²`; the regression
is run on the replicate observations, and the deviation from linearity is
`DL(x) = polynomial(x) − straight-line(x)` at each assigned concentration. Degree selection uses
the lack-of-fit F test of §3.8.

### 7.3 Limit of quantification — `Linearity.LimitOfQuantification`

- Per material `CV = 100·s/mean`; CV confidence interval
  `CV·√((n−1)/χ²(1−α/2, n−1)) … CV·√((n−1)/χ²(α/2, n−1))`.
- Fit `CV(x)` by weighted polynomial regression and find the crossing with the target CV
  (optionally the upper 95% confidence curve): `LOQ = min{x : CV_fit(x) + t·SE(x) < target}`,
  found by bisection to a relative tolerance of 1e-9.

## 8. Detection capability — `Detection.LimitOfBlank`

```
LOB = mean(blank) + z·SD(blank)          z = 1.645 (95%); legacy EP rule z = 2
LOD = LOB + z·SD(low sample)             z = 1.645 (95%)
P(detect at x) = 1 − Φ((LOD − x)/SD_low)
```

EP17 recommends at least 20 blank and 20 low-level samples.

## 9. ROC / diagnostic accuracy — `Roc.RocAnalysis`

- Kernel `ψ(X,Y) = 0.5` when `|X−Y| < 1e-5`, `1` when `Y > X`, else `0`.
- `AUC = (1/(m·n)) Σ_i Σ_j ψ(negative_j, positive_i)`; if `AUC < 0.5` the complement is used
  (direction flip).
- DeLong variance: with row means `V10_i` and column means `V01_j`,
  `Var(AUC) = Σ(V10_i − AUC)²/(m−1)/m + Σ(V01_j − AUC)²/(n−1)/n`.
- DeLong covariance between two AUCs on the same subjects:
  `Cov = cov10/m + cov01/n` from the products of the row/column mean deviations.
- Two-curve comparison: `diff = AUC₁ − AUC₂`, `r = Cov/√(V₁V₂)`,
  `SE(diff) = √(V₁ + V₂ − 2Cov)`, `z = diff/SE`, `p₂ = 2(1 − Φ(|z|))`.
- Hanley–McNeil SE from `Q₁ = AUC/(2−AUC)` and `Q₂ = 2·AUC²/(1+AUC)`.
- Confusion matrix: accuracy `(TP+TN)/N`, sensitivity `TP/(TP+FN)`, specificity `TN/(TN+FP)`,
  `PV+ = TP/(TP+FP)`, `PV− = TN/(TN+FN)`, `LR+ = sens/(1−spec)`, `LR− = (1−sens)/spec`,
  Youden `sens + spec − 1`, kappa `(pₒ − pₑ)/(1 − pₑ)`.
- Sens/spec CIs: exact Clopper–Pearson or Wilson score (preference switch).
- Cutoff table with the best (Youden) cutoff.

## 10. Reference intervals — `ReferenceIntervals.ReferenceIntervals`

- **Verification (VRI)**: at least 20 samples; pass when fewer than 10% of results fall outside
  the proposed interval.
- **Non-parametric**: limits at the 2.5th/97.5th percentiles; 90% confidence limits of each
  percentile from the order-statistic ranks of §1.2.
- **Parametric**: `x̄ ± z·s` with `z = 1.96` (95%); the 90% CI of each limit uses
  `SE_limit = s·√(1/n + z²/(2(n−1)))` and `t(0.90, n−1)`.
- **Log-transformed**: the parametric interval computed on `ln x` and exponentiated.
- Confidence ratio `= 0.5·(width(LowerCI) + width(UpperCI))/(Upper − Lower)`; values > 0.30 are
  considered too wide.
- **Harris–Boyd partitioning**: `z = |x̄₁ − x̄₂|/√(s₁²/n₁ + s₂²/n₂)`; partition when `z > 3` and the
  mean difference exceeds 25% of the total 95% reference width.
- **Reed/Dixon outlier** test with the classic critical values (n = 3…30).

## 11. Coagulation / INR — `Coagulation.Inr`

```
MNPT = exp(mean(ln PT))
INR  = (PT/MNPT)^ISI
PT limits: MNPT/GSD^m … MNPT·GSD^m   (m = 2 by default)
```

Percent outside and pass when `< 10%`. Manual INR check passes when
`|INR_computed − INR_entered|·10^decimals < 1.5` (half-unit rounding guard), preliminary below 3 items.
Calibration verification: through-origin predicted value `b·x`, allowable-error delta `k·x·factor`,
error index `(observed − predicted)/allowable` passes when `|index| ≤ 1`.

## 12. Trueness and quality goals — `Trueness.Trueness`

- Trueness regression `y = a + b·x` (OLS), correlation `r = Sxy/√(Sxx·Syy)`, bias = mean difference.
- Uncertainty `u = k·SD` (k = 2), `%u = 100·u/|mean|`; minimum 20 points per level.
- Ricos/EFLM biological-variation goals:
  `CVa = F·0.25·CVi`, `Ba = F·0.125·√(CVi² + CVg²)`, `TEa = Z·CVa + Ba`,
  with `F = 1/2/3` (optimal/desirable/minimal) and `Z = 1.65` (95%) or `2.33` (99%).
- Six Sigma `σ = (TEa − |bias|)/CV`; classification ≥6 excellent, 4–6 good, 3–4 fair,
  2–3 marginal, <2 unacceptable.

## 13. QC evaluations — `QualityControl.QcEvaluations`

- **Carryover** `= mean(high→low) − mean(low→low)`; limit defaults to `3·SD(low→low)`;
  passes when `|carryover| ≤ limit`.
- **Interference (EP7)**: paired difference `mean(X) − mean(0)` with a t CI; fitted dose-response
  effect `fit(X) − fit(0)`.
- **Stability**: regression of value (or `ln value` for first-order kinetics) on time; the slope is
  significant when its CI excludes zero; crossing time from the fitted line to the allowable drift.
- **Instrument comparison**: one-way ANOVA (§5); EP26/2IC error index `(Y − X)/rejection limit`,
  failing when `|EI| > 1` (any point for N < 20, more than 5% for N ≥ 20).

## 14. Cost per test — `Costs.CostPerTest`

```
labour      = seconds/60 · wage/benefit rate
materials   = quantity · unit cost
weekly→monthly = weekly cost · 52/12
annual→monthly = annual cost / 12
equipment per test = annual equipment cost / annual tests
indirect allocation = indirect total · direct cost / total direct cost
break-even volume = fixed cost / (revenue per test − variable cost per test)
```

## 15. Module → class map

| module / protocol | implementing class |
|---|---|
| Simple precision (SP) | `Precision.SimplePrecision` |
| Complex precision (EP5-A2) | `Precision.ComplexPrecision` |
| EP10 | `Precision.Ep10` |
| Linearity & calibration verification (LIN) | `Linearity.ClinicalLinearity` |
| EP6 linearity | `Linearity.Ep6Linearity` |
| LOQ / functional sensitivity | `Linearity.LimitOfQuantification` |
| LOB / LOD (EP17) | `Detection.LimitOfBlank` |
| AMC / EP9-A2 / EP9-A3 | `MethodComparison.MethodComparisonStats`, `MethodComparison.Ep9A3Regressions` |
| Passing–Bablok | `Regression.PassingBablok` |
| Deming / OLS regression | `Regression.RegressionMath` |
| Polynomial / lack-of-fit | `Regression.PolynomialRegression` |
| ANOVA (2IC / MIC / EP26) | `MethodComparison.Anova` |
| ROC / QMC / HMC utility | `Roc.RocAnalysis` |
| Reference intervals (VRI / ERI / C28-A2 / EP28-A3c) | `ReferenceIntervals.ReferenceIntervals` |
| INR geometric mean / manual check / PT-INR comparison | `Coagulation.Inr` |
| Trueness / uncertainty / Ricos goals / sigma | `Trueness.Trueness` |
| Carryover / interference (EP7) / stability | `QualityControl.QcEvaluations` |
| Cost per test (CPT) | `Costs.CostPerTest` |
| Descriptive foundations | `Descriptive.StatUnit`, `Descriptive.StatUnitEx`, `Descriptive.RobustStatistics` |
| Distributions and intervals | `Distributions.StatDistributions` |

## 16. Verification

`TestingStatistics/SelfTest.cs` contains 98 known-value checks across every family (χ² factors from
the EP5 guide table, exact binomial limits, Deming/Passing–Bablok on exact lines, EP5 variance
components from a constructed nested design, AUC/DeLong, INR, Ricos goals, costs, …).
`TestingStatistics/FormulaShowcase.cs` prints a worked example per module.

Run both with:

```
dotnet run --project TestingStatistics/TestingStatistics.csproj
```
