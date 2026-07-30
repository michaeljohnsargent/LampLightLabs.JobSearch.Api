import { useEffect, useRef, useState } from 'react'

interface Props {
  score: number
}

const SIZE = 208
const STROKE = 12
const RADIUS = (SIZE - STROKE) / 2
const CIRCUMFERENCE = 2 * Math.PI * RADIUS
const DURATION_MS = 900

function bandColor(score: number): string {
  if (score >= 75) return 'var(--success)'
  if (score >= 45) return 'var(--warning)'
  return 'var(--destructive)'
}

export default function ScoreGauge({ score }: Props) {
  const [display, setDisplay] = useState(0)
  const frameRef = useRef<number>()

  useEffect(() => {
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    if (reduceMotion) {
      setDisplay(score)
      return
    }

    const start = performance.now()

    function tick(now: number) {
      const t = Math.min((now - start) / DURATION_MS, 1)
      const eased = 1 - Math.pow(1 - t, 3)
      setDisplay(Math.round(eased * score))
      if (t < 1) frameRef.current = requestAnimationFrame(tick)
    }

    frameRef.current = requestAnimationFrame(tick)
    return () => {
      if (frameRef.current !== undefined) cancelAnimationFrame(frameRef.current)
    }
  }, [score])

  const color = bandColor(score)
  const dashOffset = CIRCUMFERENCE * (1 - display / 100)

  return (
    <div className="score-gauge" role="img" aria-label={`Match score: ${score} out of 100`}>
      <svg width={SIZE} height={SIZE} viewBox={`0 0 ${SIZE} ${SIZE}`}>
        <circle
          className="score-gauge-track"
          cx={SIZE / 2}
          cy={SIZE / 2}
          r={RADIUS}
          strokeWidth={STROKE}
          fill="none"
        />
        <circle
          cx={SIZE / 2}
          cy={SIZE / 2}
          r={RADIUS}
          strokeWidth={STROKE}
          fill="none"
          stroke={color}
          strokeLinecap="round"
          strokeDasharray={CIRCUMFERENCE}
          strokeDashoffset={dashOffset}
          transform={`rotate(-90 ${SIZE / 2} ${SIZE / 2})`}
        />
      </svg>
      <div className="score-gauge-label">
        <span className="score-gauge-number">{display}</span>
        <span className="score-gauge-denom">/ 100</span>
      </div>
    </div>
  )
}
