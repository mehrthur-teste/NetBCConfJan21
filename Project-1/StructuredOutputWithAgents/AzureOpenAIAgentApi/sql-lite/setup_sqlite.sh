#!/bin/bash

set -e

echo "🔧 Installing SQLite..."

# Detect OS
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    sudo apt update
    sudo apt install -y sqlite3
elif [[ "$OSTYPE" == "darwin"* ]]; then
    if ! command -v brew &> /dev/null; then
        echo "❌ Homebrew not found. Install Homebrew first."
        exit 1
    fi
    brew install sqlite
else
    echo "❌ Unsupported OS"
    exit 1
fi

echo "📁 Creating database folder..."
mkdir -p database

echo "📝 Creating init.sql..."
cat <<EOF > database/init.sql
CREATE TABLE IF NOT EXISTS employee (
    Name TEXT,
    Age INTEGER,
    Occupation TEXT
);

INSERT INTO employee (Name, Age, Occupation) VALUES
('John Doe', 30, 'Software Engineer'),
('Jane Smith', 28, 'Data Analyst'),
('Alice Johnson', 35, 'Product Manager');
EOF

echo "🗄️ Creating SQLite database and running init.sql..."
sqlite3 database/app.db < database/init.sql

echo "✅ SQLite setup completed!"
echo "📍 Database path: database/app.db"
