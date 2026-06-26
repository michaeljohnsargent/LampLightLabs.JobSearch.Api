import { useState } from 'react'
import './App.css'

const API_URL = 'https://lamplightlabs-api.azurewebsites.net/api/v2/rag/match'

interface RagMatchResponse {
  matchScore: number
  summary: string
  strengths: string[]
  gaps: string[]
  retrievedContext: string[]
}

function ScoreBadge({ score }: { score: number }) {
  const colour =
    score >= 75 ? 'score-high' : score >= 50 ? 'score-mid' : 'score-low'
  return (
    <div className={`score-badge ${colour}`}>
      <span className="score-number">{score}</span>
      <span className="score-denom">/&nbsp;100</span>
    </div>
  )
}

export default function App() {
  const [jobDescription, setJobDescription] = useState('')
  const [result, setResult] = useState<RagMatchResponse | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setLoading(true)
    setError(null)
    setResult(null)

    try {
      const res = await fetch(API_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ jobDescription }),
      })

      if (!res.ok) {
        const text = await res.text()
        throw new Error(`${res.status} ${res.statusText}${text ? ` — ${text}` : ''}`)
      }

      setResult(await res.json())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An unexpected error occurred.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main>
      <header>
        <h1>Resume Match Analyzer</h1>
        <p className="subtitle">
          Paste a job description to see how well your resume matches.
        </p>
      </header>

      <form onSubmit={handleSubmit}>
        <label htmlFor="jd">Job description</label>
        <textarea
          id="jd"
          value={jobDescription}
          onChange={e => setJobDescription(e.target.value)}
          placeholder="Paste the full job description here…"
          rows={12}
          required
          disabled={loading}
        />
        <button type="submit" disabled={loading || !jobDescription.trim()}>
          {loading ? 'Analyzing…' : 'Analyze match'}
        </button>
      </form>

      {error && (
        <div className="error-box" role="alert">
          <strong>Error:</strong> {error}
        </div>
      )}

      {result && (
        <section className="results">
          <ScoreBadge score={result.matchScore} />

          <div className="result-section">
            <h2>Summary</h2>
            <p>{result.summary}</p>
          </div>

          <div className="result-columns">
            <div className="result-section">
              <h2>Strengths</h2>
              <ul>
                {result.strengths.map((s, i) => <li key={i}>{s}</li>)}
              </ul>
            </div>

            <div className="result-section">
              <h2>Gaps</h2>
              <ul>
                {result.gaps.map((g, i) => <li key={i}>{g}</li>)}
              </ul>
            </div>
          </div>
        </section>
      )}
    </main>
  )
}
