import {
  ActiveStream,
  Channel,
  ChannelSummary,
  HdHomeRunInfo,
  HdHomeRunSettings,
  Mux,
  PredefinedMuxLocation,
  TunerAssignment,
  TunerListItem,
  TunerWithStatus,
} from "./types";

export const API = process.env.NEXT_PUBLIC_API_URL!;

// Generic API caller
export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API}/api${path}`, {
    ...init,
    // Needed for SSR and server components
    cache: "no-store"
  });

  if (!res.ok) {
    throw new Error(`API Error ${res.status}: ${res.statusText}`);
  }

  const json = await res.json();



  return json.data as T;
}

export const getTuners = () => api<TunerListItem[]>("/tuners");

export const getTuner = (id: string) => api<TunerWithStatus>(`/tuners/${id}`);

export const getMuxes = () => api<Mux[]>("/muxes");

export const getPredefinedMuxes = () => api<PredefinedMuxLocation[]>("/muxes/predefined");

export const getChannels = () => api<Channel[]>("/channels");

export const getChannelSummary = () => api<ChannelSummary>("/channels/summary");

export const scanMux = (frequency: number) =>
  api<Mux>("/muxes/scan", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ frequency }),
  });

export const getHdHomeRunInfo = () =>
  api<HdHomeRunInfo>("/integrations/hdhomerun");

export const updateHdHomeRunSettings = (payload: HdHomeRunSettings) =>
  api<HdHomeRunSettings>("/integrations/hdhomerun/settings", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });

export const getTunerAssignments = () =>
  api<TunerAssignment[]>("/tuner-assignments");

export const assignTuner = (payload: { tunerId: string; frequency: number; label?: string }) =>
  api<TunerAssignment>("/tuner-assignments", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });

export const unassignTuner = (tunerId: string) =>
  api<{ tunerId: string }>(`/tuner-assignments/${tunerId}`, {
    method: "DELETE",
  });

export const getActiveStreams = () =>
  api<ActiveStream[]>("/streams/active");
