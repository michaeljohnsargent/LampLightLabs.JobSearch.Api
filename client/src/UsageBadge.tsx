import { useEffect, useState } from 'react'

const USAGE_URL = 'https://lamplightlabs-api.azurewebsites.net/api/rag/usage'
const WARNING_THRESHOLD_PERCENT = 80

interface UsageSummary {
  totalCostUsd: number
  percentOfBudgetUsed: number
  hasHitHardCeiling: boolean
}

// Deliberately minimal: a single attention-getting badge, not a dashboard. Renders nothing
// unless usage is actually worth surfacing (near or over the soft budget), and fails silently
// on fetch errors rather than showing anything broken to a visitor.
export default function UsageBadge() {
  const [usage, setUsage] = useState<UsageSummary | null>(null)

  useEffect(() => {
    let cancelled = false

    fetch(USAGE_URL)
      .then(res => (res.ok ? res.json() : null))
      .then((data: UsageSummary | null) => {
        if (!cancelled) setUsage(data)
      })
      .catch(() => {
        if (!cancelled) setUsage(null)
      })

    return () => {
      cancelled = true
    }
  }, [])

  if (!usage) return null
  if (!usage.hasHitHardCeiling && usage.percentOfBudgetUsed < WARNING_THRESHOLD_PERCENT) return null

  const variant = usage.hasHitHardCeiling ? 'usage-badge--destructive' : 'usage-badge--warning'
  const message = usage.hasHitHardCeiling
    ? 'Monthly usage limit reached — showing sample results only.'
    : `Approaching monthly usage limit (${Math.round(usage.percentOfBudgetUsed)}% used).`

  return (
    <div className={`usage-badge ${variant}`} role="status">
      {message}
    </div>
  )
}
