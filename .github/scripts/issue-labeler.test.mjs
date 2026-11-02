import assert from "node:assert/strict";
import test from "node:test";

import { filterRequestedLabels } from "./issue-labeler.mjs";

const allowedLabels = new Set([
  "area/mtp-retry",
  "type/regression",
  "external/dotnet-test",
  "area/timeout",
  "area/mstest",
]);

test("filters a forbidden issue type without dropping valid labels", () => {
  assert.deepEqual(
    filterRequestedLabels("area/mtp-retry,type/task", allowedLabels),
    {
      acceptedLabels: ["area/mtp-retry"],
      invalidLabels: ["type/task"],
      excessLabels: [],
    },
  );
});

test("trims labels and removes duplicates", () => {
  assert.deepEqual(
    filterRequestedLabels(" area/mtp-retry,area/mtp-retry, type/regression ", allowedLabels),
    {
      acceptedLabels: ["area/mtp-retry", "type/regression"],
      invalidLabels: [],
      excessLabels: [],
    },
  );
});

test("limits accepted labels without failing the operation", () => {
  assert.deepEqual(
    filterRequestedLabels(
      "area/mtp-retry,type/regression,external/dotnet-test,area/timeout,area/mstest",
      allowedLabels,
    ),
    {
      acceptedLabels: [
        "area/mtp-retry",
        "type/regression",
        "external/dotnet-test",
        "area/timeout",
      ],
      invalidLabels: [],
      excessLabels: ["area/mstest"],
    },
  );
});

test("accepts an empty label selection", () => {
  assert.deepEqual(
    filterRequestedLabels("", allowedLabels),
    {
      acceptedLabels: [],
      invalidLabels: [],
      excessLabels: [],
    },
  );
});
