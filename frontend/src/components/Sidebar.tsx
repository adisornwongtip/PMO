import Link from "next/link";
import { 
  LayoutDashboard, 
  ArrowLeftRight, 
  GitFork, 
  FileCheck2, 
  Code2, 
  ShieldCheck,
  CreditCard
} from "lucide-react";

const navigation = [
  { name: "Dashboard Overview", href: "/", icon: LayoutDashboard },
  { name: "Live Transactions", href: "/transactions", icon: ArrowLeftRight },
  { name: "Smart Routing", href: "/routing-rules", icon: GitFork },
  { name: "Reconciliation OS", href: "/reconciliation", icon: FileCheck2 },
  { name: "API Reference & Spec", href: "/api-docs", icon: Code2 },
  { name: "Developer Settings", href: "/developers", icon: ShieldCheck },
];

export function Sidebar() {
  return (
    <aside className="w-64 border-r border-slate-800 bg-slate-950 flex flex-col justify-between p-4 min-h-screen text-slate-300">
      <div>
        <div className="flex items-center gap-3 px-2 py-4 mb-4 border-b border-slate-800">
          <div className="bg-indigo-600 p-2 rounded-xl text-white shadow-lg shadow-indigo-500/30">
            <CreditCard className="w-6 h-6" />
          </div>
          <div>
            <h1 className="font-bold text-white text-lg tracking-tight">PayFlow OS</h1>
            <p className="text-xs text-indigo-400 font-medium">Orchestration & Reconcile</p>
          </div>
        </div>

        <div className="mb-6 px-3 py-2 bg-indigo-950/40 border border-indigo-800/50 rounded-lg text-xs flex items-center justify-between">
          <span className="text-indigo-300 font-medium">Environment:</span>
          <span className="bg-emerald-500/20 text-emerald-400 px-2 py-0.5 rounded-full font-semibold border border-emerald-500/30">
            Sandbox & Live
          </span>
        </div>

        <nav className="space-y-1">
          {navigation.map((item) => {
            const Icon = item.icon;
            return (
              <Link
                key={item.name}
                href={item.href}
                className="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium hover:bg-slate-900 hover:text-white transition-colors"
              >
                <Icon className="w-4 h-4 text-slate-400" />
                <span>{item.name}</span>
              </Link>
            );
          })}
        </nav>
      </div>

      <div className="p-3 bg-slate-900/60 border border-slate-800 rounded-xl">
        <div className="flex items-center gap-2 text-xs text-emerald-400 font-medium mb-1">
          <ShieldCheck className="w-4 h-4" />
          <span>Non-Custodial Architecture</span>
        </div>
        <p className="text-[11px] text-slate-400 leading-relaxed">
          Zero merchant funds held. Transactions settle directly via licensed PSPs.
        </p>
      </div>
    </aside>
  );
}
