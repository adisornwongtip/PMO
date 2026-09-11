"use client";

import { useState } from "react";
import { GitFork, Plus, CheckCircle2, ArrowRight, ShieldCheck, ToggleLeft, ToggleRight } from "lucide-react";

export default function RoutingRulesPage() {
  const [rules, setRules] = useState([
    {
      id: "rule_1",
      priority: 1,
      name: "PromptPay High Volume QR",
      method: "PromptPay QR",
      minAmount: "Any",
      maxAmount: "Any",
      primary: "Opn",
      fallback: "GB Prime Pay",
      enabled: true
    },
    {
      id: "rule_2",
      priority: 2,
      name: "Tier 1 Credit Cards (Fast Checkout)",
      method: "Credit Card",
      minAmount: "฿100.00",
      maxAmount: "฿50,000.00",
      primary: "Opn",
      fallback: "MockSandbox",
      enabled: true
    },
    {
      id: "rule_3",
      priority: 3,
      name: "Large Amount Transactions (Low MDR)",
      method: "Credit Card",
      minAmount: "฿50,000.00",
      maxAmount: "Unlimited",
      primary: "GB Prime Pay",
      fallback: "Opn",
      enabled: true
    }
  ]);

  return (
    <div className="space-y-6 max-w-7xl mx-auto">
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white tracking-tight">Smart Routing & Failover Rules</h1>
          <p className="text-sm text-slate-400">Configure how transactions are dynamically routed across payment providers.</p>
        </div>
        <button className="flex items-center gap-2 px-3.5 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold shadow-md shadow-indigo-600/30 transition-colors">
          <Plus className="w-4 h-4" />
          <span>New Routing Rule</span>
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="p-4 bg-slate-900/40 border border-slate-800 rounded-xl">
          <div className="text-xs text-slate-400 mb-1">Active Rules</div>
          <div className="text-2xl font-bold text-white">{rules.filter(r => r.enabled).length}</div>
        </div>
        <div className="p-4 bg-slate-900/40 border border-slate-800 rounded-xl">
          <div className="text-xs text-slate-400 mb-1">Failover Coverage</div>
          <div className="text-2xl font-bold text-emerald-400">100%</div>
        </div>
        <div className="p-4 bg-slate-900/40 border border-slate-800 rounded-xl">
          <div className="text-xs text-slate-400 mb-1">Default Fallback Provider</div>
          <div className="text-2xl font-bold text-indigo-400">GB Prime Pay</div>
        </div>
      </div>

      {/* Rules Table */}
      <div className="bg-slate-900/40 border border-slate-800 rounded-2xl overflow-hidden">
        <div className="p-4 border-b border-slate-800 bg-slate-950/40 flex items-center justify-between">
          <h2 className="text-sm font-semibold text-white">Priority Ordered Rule Chain</h2>
          <span className="text-xs text-slate-400">Evaluated from top to bottom (Priority 1 = Highest)</span>
        </div>

        <table className="w-full text-left text-xs text-slate-300">
          <thead className="text-[11px] uppercase tracking-wider text-slate-500 border-b border-slate-800 bg-slate-950/20">
            <tr>
              <th className="py-3 px-4">Priority</th>
              <th className="py-3 px-4">Rule Name</th>
              <th className="py-3 px-4">Payment Method</th>
              <th className="py-3 px-4">Amount Range</th>
              <th className="py-3 px-4">Primary Provider</th>
              <th className="py-3 px-4">Fallback Provider</th>
              <th className="py-3 px-4">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/60">
            {rules.map((r) => (
              <tr key={r.id} className="hover:bg-slate-900/50 transition-colors">
                <td className="py-3.5 px-4 font-bold text-indigo-400">#{r.priority}</td>
                <td className="py-3.5 px-4 font-medium text-white">{r.name}</td>
                <td className="py-3.5 px-4">{r.method}</td>
                <td className="py-3.5 px-4 font-mono text-slate-400">{r.minAmount} - {r.maxAmount}</td>
                <td className="py-3.5 px-4">
                  <span className="px-2 py-0.5 rounded bg-indigo-950/50 text-indigo-300 border border-indigo-800/40 font-semibold">
                    {r.primary}
                  </span>
                </td>
                <td className="py-3.5 px-4">
                  <span className="px-2 py-0.5 rounded bg-slate-800 text-slate-300 border border-slate-700/50 font-medium">
                    {r.fallback}
                  </span>
                </td>
                <td className="py-3.5 px-4">
                  <button 
                    onClick={() => {
                      setRules(rules.map(x => x.id === r.id ? { ...x, enabled: !x.enabled } : x));
                    }}
                    className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded text-xs font-semibold ${r.enabled ? "text-emerald-400 bg-emerald-500/10" : "text-slate-500 bg-slate-800"}`}
                  >
                    {r.enabled ? "Active" : "Disabled"}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
