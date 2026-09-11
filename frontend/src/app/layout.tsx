import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";
import { PortalShell } from "@/components/PortalShell";

const inter = Inter({ subsets: ["latin"] });

export const metadata: Metadata = {
  title: "PayFlow OS — Payment Orchestration & Reconciliation Platform",
  description: "One API. Every Payment. Smartest Route. Enterprise Payment Orchestration & Automated Reconciliation Engine.",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en" className="dark">
      <body className={`${inter.className} min-h-screen bg-[#080B11] antialiased`}>
        <PortalShell>{children}</PortalShell>
      </body>
    </html>
  );
}
