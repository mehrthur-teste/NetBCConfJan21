#!/bin/bash
# Test GitHub Models API access
TOKEN=""

curl -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"messages":[{"role":"user","content":"Hello"}],"model":"openai/gpt-4o-mini","max_tokens":10}' \
     https://models.github.ai/inference/chat/completions