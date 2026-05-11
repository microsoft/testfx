/-
  FVSquad.TreeNodeFilter
  Formal specification of TreeNodeFilter.MatchFilterPattern from
  src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/TreeNodeFilter.cs

  🔬 Lean Squad — auto-generated formal-verification artifact.
  Target ID: 7  |  Phase: 3 (Lean 4 formal spec)  |  Date: 2026-05-07

  ## What this file contains
  - A Lean 4 model of the FilterExpression abstract syntax tree
  - An opaque Bool-valued abstract glob predicate
  - Recursive evaluators (mutual block) mirroring MatchFilterPattern
  - Proved theorems for Boolean-algebra invariants B1-B12 from informal spec

  ## Approximations / limitations
  1. Regex matching is abstracted as opaque Bool function `matchesGlob`.
  2. Property matching is abstracted as String -> Bool inside `withProps`.
  3. MatchesFilter (the public entry point) is not modelled here.
  4. Mutual recursion (evalFilter / evalFilterAll / evalFilterAny) is used
     for structural termination.

  ## Lean 4 notation note
  In Lean 4, `=` has precedence 50 and `&&`/`||` have precedence 35, so
  `a = b && c` parses as `(a = b) && c`. All Bool equalities involving `&&`
  or `||` on the right side are explicitly parenthesised in this file.
-/

-- §1  Abstract glob predicate
opaque matchesGlob : String -> String -> Bool

-- §2  FilterExpression abstract syntax tree
inductive FilterExpr : Type where
  | leaf (pattern : String) : FilterExpr
  | nop : FilterExpr
  | and (subExprs : List FilterExpr) : FilterExpr
  | or  (subExprs : List FilterExpr) : FilterExpr
  | not (inner : FilterExpr) : FilterExpr
  | withProps (value : FilterExpr) (propPred : String -> Bool) : FilterExpr

-- §3  Core evaluator (mutual block for structural termination)
mutual
def evalFilter : FilterExpr -> String -> Bool
  | .leaf p,        s => matchesGlob p s
  | .nop,           _ => true
  | .and es,        s => evalFilterAll es s
  | .or es,         s => evalFilterAny es s
  | .not inner,     s => ! evalFilter inner s
  | .withProps v f, s => evalFilter v s && f s

def evalFilterAll : List FilterExpr -> String -> Bool
  | [],      _ => true
  | e :: es, s => evalFilter e s && evalFilterAll es s

def evalFilterAny : List FilterExpr -> String -> Bool
  | [],      _ => false
  | e :: es, s => evalFilter e s || evalFilterAny es s
end

-- §4  Definitional equation lemmas (rw [...eq_def] closes via match reduction)
@[simp] theorem evalFilter_leaf_eq (p s : String) :
    evalFilter (.leaf p) s = matchesGlob p s := by rw [evalFilter.eq_def]

@[simp] theorem evalFilter_nop_eq (s : String) :
    evalFilter .nop s = true := by rw [evalFilter.eq_def]

@[simp] theorem evalFilter_and_eq (es : List FilterExpr) (s : String) :
    evalFilter (.and es) s = evalFilterAll es s := by rw [evalFilter.eq_def]

@[simp] theorem evalFilter_or_eq (es : List FilterExpr) (s : String) :
    evalFilter (.or es) s = evalFilterAny es s := by rw [evalFilter.eq_def]

@[simp] theorem evalFilter_not_eq (e : FilterExpr) (s : String) :
    evalFilter (.not e) s = ! evalFilter e s := by rw [evalFilter.eq_def]

-- Note: RHS parenthesised to avoid `=` binding tighter than `&&` (prec 50 > 35)
@[simp] theorem evalFilter_withProps_eq (v : FilterExpr) (f : String -> Bool) (s : String) :
    evalFilter (.withProps v f) s = (evalFilter v s && f s) := by rw [evalFilter.eq_def]

@[simp] theorem evalFilterAll_nil (s : String) :
    evalFilterAll [] s = true := by rw [evalFilterAll.eq_def]

-- Note: RHS parenthesised
@[simp] theorem evalFilterAll_cons (e : FilterExpr) (es : List FilterExpr) (s : String) :
    evalFilterAll (e :: es) s = (evalFilter e s && evalFilterAll es s) := by
  rw [evalFilterAll.eq_def]

@[simp] theorem evalFilterAny_nil (s : String) :
    evalFilterAny [] s = false := by rw [evalFilterAny.eq_def]

-- Note: RHS parenthesised
@[simp] theorem evalFilterAny_cons (e : FilterExpr) (es : List FilterExpr) (s : String) :
    evalFilterAny (e :: es) s = (evalFilter e s || evalFilterAny es s) := by
  rw [evalFilterAny.eq_def]

-- §5  De Morgan helpers
-- Note: LHS `(!evalFilterAny es s)` is parenthesised to force Bool.not reading
-- Note: RHS is Bool conjunction, so parenthesised

theorem evalFilterAny_not_eq_all (es : List FilterExpr) (s : String) :
    (!evalFilterAny es s) = evalFilterAll (es.map .not) s := by
  induction es with
  | nil => simp
  | cons h t ih =>
    simp only [evalFilterAny_cons, evalFilterAll_cons, List.map_cons]
    rw [Bool.not_or, ← evalFilter_not_eq]
    exact congrArg (evalFilter (.not h) s && ·) ih

theorem evalFilterAll_not_eq_any (es : List FilterExpr) (s : String) :
    (!evalFilterAll es s) = evalFilterAny (es.map .not) s := by
  induction es with
  | nil => simp
  | cons h t ih =>
    simp only [evalFilterAll_cons, evalFilterAny_cons, List.map_cons]
    rw [Bool.not_and, ← evalFilter_not_eq]
    exact congrArg (evalFilter (.not h) s || ·) ih

-- §6  Boolean-algebra theorems B1-B12

-- B1: NopExpression always returns true
theorem evalFilter_nop (s : String) : evalFilter .nop s = true := by simp

-- B2: Double negation elimination
theorem evalFilter_not_not (e : FilterExpr) (s : String) :
    evalFilter (.not (.not e)) s = evalFilter e s := by
  simp [Bool.not_not]

-- B3: De Morgan — not(or es) = and(map not es)
theorem evalFilter_not_or_eq_and_map_not (es : List FilterExpr) (s : String) :
    evalFilter (.not (.or es)) s = evalFilter (.and (es.map .not)) s := by
  simp only [evalFilter_not_eq, evalFilter_or_eq, evalFilter_and_eq]
  exact evalFilterAny_not_eq_all es s

-- B4: De Morgan — not(and es) = or(map not es)
theorem evalFilter_not_and_eq_or_map_not (es : List FilterExpr) (s : String) :
    evalFilter (.not (.and es)) s = evalFilter (.or (es.map .not)) s := by
  simp only [evalFilter_not_eq, evalFilter_and_eq, evalFilter_or_eq]
  exact evalFilterAll_not_eq_any es s

-- B5: And commutativity (two-element list)
theorem evalFilter_and_comm (a b : FilterExpr) (s : String) :
    evalFilter (.and [a, b]) s = evalFilter (.and [b, a]) s := by
  simp [Bool.and_comm]

-- B6: Or commutativity (two-element list)
theorem evalFilter_or_comm (a b : FilterExpr) (s : String) :
    evalFilter (.or [a, b]) s = evalFilter (.or [b, a]) s := by
  simp [Bool.or_comm]

-- B7: Singleton And
theorem evalFilter_and_singleton (e : FilterExpr) (s : String) :
    evalFilter (.and [e]) s = evalFilter e s := by simp

-- B8: Singleton Or
theorem evalFilter_or_singleton (e : FilterExpr) (s : String) :
    evalFilter (.or [e]) s = evalFilter e s := by simp

-- B9: Nop is the And-identity
theorem evalFilter_and_nop_left (e : FilterExpr) (s : String) :
    evalFilter (.and [.nop, e]) s = evalFilter e s := by simp

-- B10: Nop absorbs Or
theorem evalFilter_or_nop_absorb (e : FilterExpr) (s : String) :
    evalFilter (.or [.nop, e]) s = true := by simp

-- B11: Vacuous And (empty conjunction is true)
theorem evalFilter_and_empty (s : String) : evalFilter (.and []) s = true := by simp

-- B12: Vacuous Or (empty disjunction is false)
theorem evalFilter_or_empty (s : String) : evalFilter (.or []) s = false := by simp

-- §7  Additional structural properties

-- Double negation of the Bool result (parenthesise `!!` to force Bool reading)
theorem evalFilter_result_double_neg (e : FilterExpr) (s : String) :
    (!! evalFilter e s) = evalFilter e s := Bool.not_not _

-- withProps fails if the glob fails
theorem evalFilter_withProps_false_glob
    (e : FilterExpr) (f : String -> Bool) (s : String)
    (h : evalFilter e s = false) :
    evalFilter (.withProps e f) s = false := by simp [h]

-- withProps fails if the property predicate fails
theorem evalFilter_withProps_false_prop
    (e : FilterExpr) (f : String -> Bool) (s : String)
    (h : f s = false) :
    evalFilter (.withProps e f) s = false := by simp [h]

-- If and [a, b] matches then a alone matches
theorem evalFilter_and_left_implies (a b : FilterExpr) (s : String)
    (h : evalFilter (.and [a, b]) s = true) : evalFilter a s = true := by
  simp at h; exact h.1

-- If and [a, b] matches then b alone matches
theorem evalFilter_and_right_implies (a b : FilterExpr) (s : String)
    (h : evalFilter (.and [a, b]) s = true) : evalFilter b s = true := by
  simp at h; exact h.2

-- If or [a, b] does not match then a alone does not match
theorem evalFilter_or_left_false (a b : FilterExpr) (s : String)
    (h : evalFilter (.or [a, b]) s = false) : evalFilter a s = false := by
  simp [Bool.or_eq_false_iff] at h; exact h.1

-- Triple negation reduces to single negation
theorem evalFilter_not_not_not (e : FilterExpr) (s : String) :
    evalFilter (.not (.not (.not e))) s = evalFilter (.not e) s := by
  simp [Bool.not_not]
