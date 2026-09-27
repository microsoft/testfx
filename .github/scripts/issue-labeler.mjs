export function filterRequestedLabels(labels, allowedLabels, maxLabels = 4) {
  const requestedLabels = [...new Set(
    labels.split(",").map(label => label.trim()).filter(Boolean),
  )];
  const acceptedLabels = [];
  const invalidLabels = [];
  const excessLabels = [];

  for (const label of requestedLabels) {
    if (!allowedLabels.has(label)) {
      invalidLabels.push(label);
    } else if (acceptedLabels.length < maxLabels) {
      acceptedLabels.push(label);
    } else {
      excessLabels.push(label);
    }
  }

  return { acceptedLabels, invalidLabels, excessLabels };
}
