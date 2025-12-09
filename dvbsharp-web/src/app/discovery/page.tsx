export const dynamic = "force-dynamic";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { PinTunerForm } from "@/components/pin-tuner-form";
import { getMuxes, getTunerAssignments, getTuners } from "@/lib/api";

export default async function DiscoveryPage() {
  const [tuners, muxes, assignments] = await Promise.all([
    getTuners().catch(() => []),
    getMuxes().catch(() => []),
    getTunerAssignments().catch(() => []),
  ]);

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <p className="text-sm uppercase tracking-[0.2em] text-muted-foreground">Discovery</p>
        <h1 className="text-2xl font-semibold">Pin Tuners to Muxes</h1>
        <p className="text-sm text-muted-foreground">
          Keep DVB-T/T2 frontends locked to specific multiplexes so they are always ready for streaming.
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Quick start</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2 text-sm text-muted-foreground">
          <p>1. Run a fake or real mux scan on the frequencies you care about.</p>
          <p>2. Choose a tuner and mux below, then click “Pin tuner”.</p>
          <p>3. Streams hitting that tuner will stay on the pinned mux; cross-tuning is blocked.</p>
        </CardContent>
      </Card>

      {tuners.length === 0 ? (
        <Card>
          <CardHeader>
            <CardTitle>No tuners registered</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Start the backend with a DVB frontend or the fake Cambridge tuner enabled to use discovery features.
            </p>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle>Pin a tuner</CardTitle>
          </CardHeader>
          <CardContent>
            <PinTunerForm tuners={tuners} muxes={muxes} assignments={assignments} />
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Known muxes</CardTitle>
          <p className="text-sm text-muted-foreground">Imported from the mux database for quick selection.</p>
        </CardHeader>
        <CardContent className="space-y-4">
          {muxes.length === 0 && <p className="text-sm text-muted-foreground">No muxes available yet.</p>}
          {muxes.slice(0, 8).map((mux) => (
            <div key={mux.id} className="rounded-md border p-3">
              <div className="flex items-center justify-between text-sm">
                <span className="font-medium">{mux.frequency.toLocaleString()} Hz</span>
                <span className="text-muted-foreground">{mux.services.length} services</span>
              </div>
              <Separator className="my-2" />
              <div className="flex flex-wrap gap-1 text-xs text-muted-foreground">
                {mux.services.slice(0, 6).map((service) => (
                  <span key={service.serviceId} className="rounded-full border px-2 py-0.5">
                    {service.name}
                  </span>
                ))}
              </div>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
