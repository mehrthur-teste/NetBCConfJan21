-- init.sql

CREATE TABLE employee (
    Name VARCHAR(100),
    Age INT,
    Occupation VARCHAR(100)
);

-- Insert sample data into the employee table
INSERT INTO employee (Name, Age, Occupation) VALUES 
('John Doe', 30, 'Software Engineer'),
('Jane Smith', 28, 'Data Analyst'),
('Alice Johnson', 35, 'Product Manager');
