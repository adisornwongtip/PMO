"use client";

import { useState } from "react";
import Link from "next/link";
import { 
  TrendingUp, 
  CheckCircle2, 
  AlertTriangle, 
  Zap, 
  ArrowUpRight, 
  RefreshCw,
  Clock,
  Play,
  ArrowRight,
  ShieldCheck,
  Sparkles,
  CreditCard,
  QrCode,
  Activity
} from "lucide-react";

export default function DashboardOverview() {
  // Live Simulation state
  const [simAmount, setSimAmount] = useState<number>(4800);
  const [simMethod, setSimMethod] = useState<number>(1); // 1 = Card, 2 = PromptPay
  const [simulateTimeout, setSimulateTimeout] = useState<boolean>(true);
  const [isSimulating, setIsSimulating] = useState<boolean>(false);
  const [simResult, setSimResult] = useState<any>(null);

  const kpis = [
    { title: "Gross Volume (GMV)", value: "฿12,845,200", change: "+14.2% vs last month", isPositive: true, icon: TrendingUp },
    { title: "Payment Success Rate", value: "98.74%", change: "+2.1% via Smart Failover", isPositive: true, icon: CheckCircle2 },
    { title: "Failed Transactions", value: "142", change: "48 recovered via Fallback", isPositive: false, icon: AlertTriangle },
    { title: "Estimated Fees Saved", value: "฿44,958", change: "Through Least-Cost Routing", isPositive: true, icon: Zap },
  ];

  const providers = [
    { name: "Opn (Omise)", role: "Tier 1 Cards & PromptPay", successRate: "98.8%", latency: "210ms", status: "Healthy", tag: "Primary" },
    { name: "GB Prime Pay", role: "Tier 2 High-Value & Thai QR", successRate: "99.2%", latency: "180ms", status: "Healthy", tag: "Fallback" },
    { name: "Mock Sandbox", role: "Chaos Simulation & Testing", successRate: "100%", latency: "120ms", status: "Active", tag: "Testing" },
  ];

  const recentTransactions = [
    { id: "txn_01J8F94821", ref: "ORD-94821", method: "PromptPay QR", amount: "฿1,500.00", provider: "Opn", status: "Success", failover: false, time: "2 mins ago" },
    { id: "txn_01J8F94822", ref: "ORD-94822", method: "Credit Card (Visa)", amount: "฿4,800.00", provider: "GB Prime Pay", status: "Success", failover: true, time: "5 mins ago" },
    { id: "txn_01J8F94823", ref: "ORD-94823", method: "Credit Card (Mastercard)", amount: "฿12,500.00", provider: "Opn", status: "Success", failover: false, time: "12 mins ago" },
    { id: "txn_01J8F94824", ref: "ORD-94824", method: "PromptPay QR", amount: "฿350.00", provider: "GB Prime Pay", status: "Success", failover: false, time: "18 mins ago" },
    { id: "txn_01J8F94825", ref: "ORD-94825", method: "Credit Card", amount: "฿950.00", provider: "MockSandbox", status: "Failed", failover: false, time: "25 mins ago" },
  ];

  const handleRunSimulation = async () => {
    setIsSimulating(true);
    setSimResult(null);

    // Simulate smart routing & failover execution
    setTimeout(() => {
      if (simulateTimeout) {
        setSimResult({
          transactionId: `txn_${Date.now().toString(36).toUpperCase()}`,
          orderRef: `ORD-SIM-${Math.floor(Math.random() * 9000 + 1000)}`,
          amount: simAmount,
          finalProvider: "GB Prime Pay",
          status: "Success",
          failoverTriggered: true,
          attempts: [
            { provider: "Opn", status: 504, latency: "2,450ms", message: "Gateway Timeout: Network unreachable" },
            { provider: "GB Prime Pay", status: 200, latency: "180ms", message: "Charge authorized successfully" }
          ],
          fee: simAmount * 0.032,
          totalLatency: "2,630ms"
        });
      } else {
        setSimResult({
          transactionId: `txn_${Date.now().toString(36).toUpperCase()}`,
          orderRef: `ORD-SIM-${Math.floor(Math.random() * 9000 + 1000)}`,
          amount: simAmount,
          finalProvider: "Opn",
          status: "Success",
          failoverTriggered: false,
          attempts: [
            { provider: "Opn", status: 200, latency: "210ms", message: "Charge authorized via primary route" }
          ],
          fee: simAmount * 0.0365,
          totalLatency: "210ms"
        });
      }
      setIsSimulating(false);
    }, 800);
  };

  return (
    <div className="space-y-8 max-w-7xl mx-auto">
      {/* Top Banner & Header */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <span className="text-xs font-semibold px-2.5 py-0.5 rounded-full bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
              One API. Every Payment. Smartest Route.
            </span>
          </div>
          <h1 className="text-2xl sm:text-3xl font-bold text-white tracking-tight">Payment Operations & Orchestration</h1>
          <p className="text-xs sm:text-sm text-slate-400">Dynamic routing, zero-downtime failover, and automated settlement audit.</p>
        </div>
        <div className="flex items-center gap-3">
          <button 
            onClick={() => window.location.reload()}
            className="flex items-center gap-2 px-3 py-1.5 text-xs font-medium text-slate-300 bg-slate-900/60 border border-white/[0.08] rounded-xl hover:bg-slate-800 hover:text-white transition-all shadow-sm"
          >
            <RefreshCw className="w-3.5 h-3.5" />
            <span>Sync</span>
          </button>
          <Link
            href="/api-docs"
            className="flex items-center gap-2 px-3.5 py-1.5 text-xs font-semibold text-white bg-gradient-to-r from-indigo-600 to-indigo-500 rounded-xl hover:from-indigo-500 hover:to-indigo-400 shadow-lg shadow-indigo-600/25 transition-all"
          >
            <span>Open API Spec & Docs</span>
            <ArrowUpRight className="w-3.5 h-3.5" />
          </Link>
        </div>
      </div>

      {/* KPI Bento Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {kpis.map((kpi, idx) => {
          const Icon = kpi.icon;
          return (
            <div 
              key={idx} 
              className="p-5 bg-slate-900/40 border border-white/[0.08] hover:border-indigo-500/30 rounded-2xl relative overflow-hidden backdrop-blur-xl transition-all duration-200 group shadow-xl"
            >
              <div className="flex items-center justify-between text-slate-400 mb-3">
                <span className="text-xs font-medium uppercase tracking-wider">{kpi.title}</span>
                <div className="p-2 bg-slate-800/40 rounded-xl text-slate-300 group-hover:text-indigo-400 group-hover:bg-indigo-500/10 transition-colors border border-white/[0.04]">
                  <Icon className="w-4 h-4" />
                </div>
              </div>
              <div className="text-2xl sm:text-3xl font-bold text-white mb-2 tracking-tight">{kpi.value}</div>
              <div className={`text-xs font-medium flex items-center gap-1 ${kpi.isPositive ? "text-emerald-400" : "text-amber-400"}`}>
                <span>{kpi.change}</span>
              </div>
            </div>
          );
        })}
      </div>

      {/* Interactive Simulation Playground + AI Insights in Bento 2-Column */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Live Simulation Card (2 Columns) */}
        <div className="lg:col-span-2 p-6 bg-slate-900/50 border border-white/[0.08] rounded-2xl backdrop-blur-xl shadow-xl relative overflow-hidden">
          <div className="flex items-center justify-between mb-4 border-b border-white/[0.06] pb-4">
            <div className="flex items-center gap-2.5">
              <div className="p-2 rounded-xl bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
                <Play className="w-4 h-4" />
              </div>
              <div>
                <h2 className="text-base font-bold text-white tracking-tight">Interactive Smart Routing Playground</h2>
                <p className="text-xs text-slate-400">Trigger live transactions and inspect multi-provider failover in real-time.</p>
              </div>
            </div>
            <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-indigo-500/10 text-indigo-400 border border-indigo-500/30 uppercase tracking-wider">
              Live Sandbox
            </span>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-4">
            <div>
              <label className="block text-[11px] font-medium text-slate-400 mb-1.5 uppercase tracking-wider">Payment Method</label>
              <div className="grid grid-cols-2 gap-2">
                <button
                  type="button"
                  onClick={() => setSimMethod(2)}
                  className={`flex items-center justify-center gap-1.5 py-2 px-3 rounded-xl text-xs font-semibold border transition-all ${
                    simMethod === 2 ? "bg-indigo-600 border-indigo-500 text-white shadow-md shadow-indigo-600/30" : "bg-slate-950/60 border-white/[0.06] text-slate-400 hover:text-white"
                  }`}
                >
                  <QrCode className="w-3.5 h-3.5" />
                  <span>PromptPay</span>
                </button>
                <button
                  type="button"
                  onClick={() => setSimMethod(1)}
                  className={`flex items-center justify-center gap-1.5 py-2 px-3 rounded-xl text-xs font-semibold border transition-all ${
                    simMethod === 1 ? "bg-indigo-600 border-indigo-500 text-white shadow-md shadow-indigo-600/30" : "bg-slate-950/60 border-white/[0.06] text-slate-400 hover:text-white"
                  }`}
                >
                  <CreditCard className="w-3.5 h-3.5" />
                  <span>Credit Card</span>
                </button>
              </div>
            </div>

            <div>
              <label className="block text-[11px] font-medium text-slate-400 mb-1.5 uppercase tracking-wider">Transaction Amount</label>
              <div className="relative">
                <span className="absolute left-3 top-1/2 -translate-y-1/2 text-xs font-semibold text-slate-400">฿</span>
                <input
                  type="number"
                  value={simAmount}
                  onChange={(e) => setSimAmount(Number(e.target.value))}
                  className="w-full bg-slate-950/70 border border-white/[0.08] focus:border-indigo-500 rounded-xl pl-7 pr-3 py-2 text-xs font-semibold text-white focus:outline-none"
                />
              </div>
            </div>

            <div>
              <label className="block text-[11px] font-medium text-slate-400 mb-1.5 uppercase tracking-wider">Chaos Injection</label>
              <button
                type="button"
                onClick={() => setSimulateTimeout(!simulateTimeout)}
                className={`w-full py-2 px-3 rounded-xl text-xs font-semibold border flex items-center justify-center gap-2 transition-all ${
                  simulateTimeout
                    ? "bg-amber-500/10 border-amber-500/30 text-amber-300"
                    : "bg-slate-950/60 border-white/[0.06] text-slate-400"
                }`}
              >
                <span className={`w-2 h-2 rounded-full ${simulateTimeout ? "bg-amber-400 animate-pulse" : "bg-slate-600"}`} />
                <span>{simulateTimeout ? "Simulate Opn 504 Timeout" : "Normal Route (Healthy)"}</span>
              </button>
            </div>
          </div>

          <div className="flex justify-end mb-4">
            <button
              onClick={handleRunSimulation}
              disabled={isSimulating}
              className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-gradient-to-r from-indigo-600 via-indigo-500 to-violet-600 hover:opacity-95 text-white text-xs font-bold shadow-lg shadow-indigo-600/30 transition-all disabled:opacity-50"
            >
              <Play className="w-3.5 h-3.5 fill-current" />
              <span>{isSimulating ? "Evaluating Route & Executing..." : "Execute Test Payment"}</span>
            </button>
          </div>

          {/* Simulation Output Card */}
          {simResult && (
            <div className="p-4 bg-slate-950/80 border border-white/[0.1] rounded-xl space-y-3 animate-in fade-in zoom-in-95 duration-200">
              <div className="flex items-center justify-between border-b border-white/[0.06] pb-2">
                <div className="flex items-center gap-2">
                  <span className="font-mono text-xs font-bold text-white">{simResult.transactionId}</span>
                  <span className="text-slate-400 text-xs font-mono">({simResult.orderRef})</span>
                  {simResult.failoverTriggered ? (
                    <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-amber-500/20 text-amber-300 border border-amber-500/30">
                      FAILOVER SUCCESSFUL
                    </span>
                  ) : (
                    <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                      PRIMARY SUCCESS
                    </span>
                  )}
                </div>
                <span className="text-xs font-mono text-slate-400">Total Latency: {simResult.totalLatency}</span>
              </div>

              <div className="space-y-2">
                {simResult.attempts.map((att: any, idx: number) => (
                  <div key={idx} className="flex items-center justify-between text-xs p-2 rounded-lg bg-slate-900/60 border border-white/[0.04]">
                    <div className="flex items-center gap-2">
                      <span className="text-slate-500 font-mono">Step {idx + 1}:</span>
                      <span className="font-semibold text-white">{att.provider}</span>
                      <span className={`px-1.5 py-0.2 rounded text-[10px] font-mono ${att.status === 200 ? "bg-emerald-500/20 text-emerald-300" : "bg-rose-500/20 text-rose-300"}`}>
                        HTTP {att.status}
                      </span>
                      <span className="text-slate-400">{att.message}</span>
                    </div>
                    <span className="text-slate-500 font-mono text-[11px]">{att.latency}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* AI Insights & Diagnostics (1 Column) */}
        <div className="p-6 bg-gradient-to-br from-indigo-950/40 via-slate-900/60 to-slate-900/40 border border-indigo-500/20 rounded-2xl backdrop-blur-xl shadow-xl space-y-4">
          <div className="flex items-center gap-2 text-indigo-400">
            <Sparkles className="w-5 h-5" />
            <h3 className="text-sm font-bold text-white">AI Routing Intelligence</h3>
          </div>
          <div className="p-3 bg-indigo-500/10 border border-indigo-500/20 rounded-xl space-y-1">
            <div className="text-xs font-semibold text-indigo-200">Least-Cost Route Optimization</div>
            <p className="text-[11px] text-slate-300 leading-relaxed">
              Transactions &gt; ฿50k routed to GB Prime Pay tiered MDR saved <strong>฿44,958</strong> in net fees this period.
            </p>
          </div>
          <div className="p-3 bg-amber-500/10 border border-amber-500/20 rounded-xl space-y-1">
            <div className="text-xs font-semibold text-amber-200">Failover Incident Prevention</div>
            <p className="text-[11px] text-slate-300 leading-relaxed">
              Automatic failover successfully preserved <strong>48 transactions (฿184,200)</strong> during Opn latency spikes.
            </p>
          </div>
          <div className="pt-2 border-t border-white/[0.06] text-[11px] text-slate-400 flex items-center justify-between">
            <span>Circuit Breaker State</span>
            <span className="text-emerald-400 font-mono font-semibold">ALL HEALTHY</span>
          </div>
        </div>
      </div>

      {/* Provider Telemetry Cards */}
      <div>
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-base font-bold text-white tracking-tight">Active Payment Rails (Telemetry)</h2>
          <span className="text-xs text-slate-400">Latency ping every 30s</span>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {providers.map((p, idx) => (
            <div key={idx} className="p-5 bg-slate-900/40 border border-white/[0.08] hover:border-indigo-500/30 rounded-2xl backdrop-blur-xl transition-all shadow-xl">
              <div className="flex items-center justify-between mb-2">
                <div className="flex items-center gap-2">
                  <h3 className="font-semibold text-white text-sm">{p.name}</h3>
                  <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-slate-800 text-indigo-300 border border-white/[0.06]">
                    {p.tag}
                  </span>
                </div>
                <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[11px] font-medium bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse" />
                  {p.status}
                </span>
              </div>
              <p className="text-xs text-slate-400 mb-4">{p.role}</p>
              <div className="grid grid-cols-2 gap-2 pt-3 border-t border-white/[0.06] text-xs">
                <div>
                  <span className="text-slate-500 block text-[10px] uppercase font-semibold">Success Rate</span>
                  <span className="text-white font-mono font-semibold">{p.successRate}</span>
                </div>
                <div>
                  <span className="text-slate-500 block text-[10px] uppercase font-semibold">Latency</span>
                  <span className="text-white font-mono font-semibold">{p.latency}</span>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Recent Transactions Table */}
      <div className="p-6 bg-slate-900/40 border border-white/[0.08] rounded-2xl backdrop-blur-xl shadow-xl">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h2 className="text-base font-bold text-white tracking-tight">Live Transaction Stream</h2>
            <p className="text-xs text-slate-400">Incoming merchant payments and dynamic routing execution.</p>
          </div>
          <a href="/transactions" className="text-xs text-indigo-400 hover:text-indigo-300 font-semibold flex items-center gap-1">
            <span>View Full Ledger</span>
            <ArrowRight className="w-3.5 h-3.5" />
          </a>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs text-slate-300">
            <thead className="text-[11px] uppercase tracking-wider text-slate-500 border-b border-white/[0.06] bg-slate-950/40">
              <tr>
                <th className="py-3 px-4">Order Ref</th>
                <th className="py-3 px-4">Payment Method</th>
                <th className="py-3 px-4">Amount</th>
                <th className="py-3 px-4">Routed Provider</th>
                <th className="py-3 px-4">Status</th>
                <th className="py-3 px-4">Time</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/[0.04]">
              {recentTransactions.map((t) => (
                <tr key={t.id} className="hover:bg-slate-800/30 transition-colors">
                  <td className="py-3.5 px-4 font-mono font-medium text-white">{t.ref}</td>
                  <td className="py-3.5 px-4">{t.method}</td>
                  <td className="py-3.5 px-4 font-semibold text-white">{t.amount}</td>
                  <td className="py-3.5 px-4">
                    <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-lg bg-slate-800/80 border border-white/[0.06] text-slate-200 font-medium">
                      {t.provider}
                      {t.failover && (
                        <span className="text-[9px] bg-amber-500/20 text-amber-300 px-1 rounded font-bold">
                          FAILOVER
                        </span>
                      )}
                    </span>
                  </td>
                  <td className="py-3.5 px-4">
                    {t.status === "Success" ? (
                      <span className="inline-flex items-center gap-1 text-emerald-400 font-medium">
                        <CheckCircle2 className="w-3.5 h-3.5" />
                        Success
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 text-rose-400 font-medium">
                        <AlertTriangle className="w-3.5 h-3.5" />
                        Failed
                      </span>
                    )}
                  </td>
                  <td className="py-3.5 px-4 text-slate-500 flex items-center gap-1 font-mono">
                    <Clock className="w-3 h-3" />
                    {t.time}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
