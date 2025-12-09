export function formatBitrate(bps: number): string {
  if (!bps || Number.isNaN(bps)) return "0 bps";
  if (bps > 1e9) return `${(bps / 1e9).toFixed(2)} Gbps`;
  if (bps > 1e6) return `${(bps / 1e6).toFixed(2)} Mbps`;
  if (bps > 1e3) return `${(bps / 1e3).toFixed(1)} kbps`;
  return `${bps.toFixed(0)} bps`;
}

export function formatDate(iso: string) {
  return new Date(iso).toLocaleString();
}

export function formatCapabilities(capabilities?: string[]) {
  if (!capabilities || capabilities.length === 0) {
    return "Unknown";
  }

  return capabilities.join(", ");
}
