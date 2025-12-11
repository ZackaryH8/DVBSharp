"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { updateHdHomeRunSettings } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

type HdHomeRunLimitFormProps = {
  initialLimit?: number | null;
};

export function HdHomeRunLimitForm({ initialLimit }: HdHomeRunLimitFormProps) {
  const [value, setValue] = useState(initialLimit ? String(initialLimit) : "");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const router = useRouter();

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();

    const trimmed = value.trim();
    let parsed: number | null = null;

    if (trimmed.length > 0) {
      const numeric = Number(trimmed);
      if (Number.isNaN(numeric) || numeric < 1 || numeric > 8) {
        setError("Enter a value between 1 and 8, or leave blank to match physical tuners.");
        return;
      }
      parsed = numeric;
    }

    setBusy(true);
    setError(null);
    try {
      await updateHdHomeRunSettings({ tunerLimit: parsed });
      router.refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save tuner limit.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-2 rounded-md border bg-muted/40 p-3 text-sm">
      <div className="flex flex-col gap-1">
        <label htmlFor="tunerLimit" className="font-medium">
          Advertised tuners
        </label>
        <Input
          id="tunerLimit"
          type="number"
          min={1}
          max={8}
          placeholder="Leave blank for automatic"
          value={value}
          onChange={(event) => setValue(event.target.value)}
        />
        <p className="text-xs text-muted-foreground">
          Control how many tuners clients discover. Leave empty to expose the physical tuner count.
        </p>
      </div>
      {error && <p className="text-xs text-destructive">{error}</p>}
      <Button type="submit" size="sm" disabled={busy}>
        {busy ? "Saving..." : "Save"}
      </Button>
    </form>
  );
}
