 "use client";

import { useState } from "react";
import { scanMux } from "@/lib/api";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import { Separator } from "./ui/separator";
import { useRouter } from "next/navigation";

export function MuxScanForm() {
  const [frequency, setFrequency] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const router = useRouter();

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    const value = Number(frequency);
    if (!value || Number.isNaN(value)) {
      setError("Enter a frequency in Hz");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await scanMux(value);
      setFrequency("");
      router.refresh();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Scan failed";
      setError(message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <form
      onSubmit={submit}
      className="flex w-full flex-col gap-3 rounded-lg border bg-card p-4 shadow-sm"
    >
      <div className="flex flex-col gap-1">
        <label className="text-sm font-medium">Scan mux (Hz)</label>
        <Input
          type="number"
          placeholder="e.g. 538000000"
          value={frequency}
          onChange={(e) => setFrequency(e.target.value)}
        />
      </div>
      {error ? <p className="text-sm text-destructive">{error}</p> : null}
      <Separator />
      <Button type="submit" disabled={busy}>
        {busy ? "Scanning..." : "Start Scan"}
      </Button>
    </form>
  );
}
